using Common;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Serilog;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace MQTTServer;

public class Server
{
    private readonly ServerInfo _serverInfo;
    private readonly MqttServerFactory _factory = new();
    private readonly X509Certificate _certificate;
    public readonly ConcurrentQueue<(string, MqttApplicationMessage)> RecvQueue = new();
    public readonly ConcurrentQueue<(bool, string)> ClientsQueue = new();
    public HashSet<string> Clients = new();
    private MqttServer _server;

    public Server(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
    }

    public async Task Run()
    {
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), _serverInfo.CertificatePath);
            var options = new MqttServerOptionsBuilder()
                .WithEncryptionCertificate(File.ReadAllBytes(path), new MqttServerCertificateCredentials() { Password = "IDoNotKnow!" })
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(_serverInfo.Port)
                .Build();
            _server = _factory.CreateMqttServer(options);
            _server.ValidatingConnectionAsync += ServerOnValidatingConnectionAsync;
            _server.ClientDisconnectedAsync += ServerOnClientDisconnectedAsync;
            _server.InterceptingPublishAsync += ServerOnInterceptingPublishAsync;
            await _server.StartAsync();

            while (true)
            {
                while (ClientsQueue.TryDequeue(out var client))
                {
                    if (client.Item1)
                    {
                        if (Clients.Contains(client.Item2)) continue;
                        Clients.Add(client.Item2);
                    }
                    else
                    {
                        Clients.Remove(client.Item2);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private Task ServerOnInterceptingPublishAsync(InterceptingPublishEventArgs arg)
    {
        RecvQueue.Enqueue((arg.ClientId, arg.ApplicationMessage));
        return Task.CompletedTask;
    }

    public async Task SendMessage(string clientId, Message message)
    {
        // Create a new message using the builder as usual.
        var mqtt = new MqttApplicationMessageBuilder()
            .WithTopic("/estation/recv")
            .WithTopicAlias(ushort.Parse(clientId))
            .WithPayload(JsonSerializer.Serialize(message))
            .Build();

        // Now inject the new message at the broker.
        await _server.InjectApplicationMessage(
            new InjectedMqttApplicationMessage(mqtt)
            {
                SenderClientId = clientId
            });
    }

    private Task ServerOnValidatingConnectionAsync(ValidatingConnectionEventArgs arg)
    {
        arg.ReasonCode = arg.UserName == _serverInfo.UserName && arg.Password == _serverInfo.Password
            ? MqttConnectReasonCode.Success
            : MqttConnectReasonCode.UnspecifiedError;

        if (arg.ReasonCode == MqttConnectReasonCode.Success)
        {
            _server.SubscribeAsync(arg.ClientId, "/estation/send");
            ClientsQueue.Enqueue((true, arg.ClientId));
        }

        return Task.CompletedTask;
    }

    private Task ServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs arg)
    {
        //Log.Information($"Client {arg.ClientId}({arg.Endpoint}) disconnected.");
        ClientsQueue.Enqueue((false, arg.ClientId));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_server.IsStarted)
            await _server.StopAsync();
    }
}

public class ServerInfo
{
    public int Port { get; set; } = 9071;
    public string UserName { get; set; } = "test";
    public string Password { get; set; } = "Pass99";
    public string CertificatePath { get; set; } = "server.crt";
}