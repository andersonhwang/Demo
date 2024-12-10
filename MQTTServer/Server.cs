using Common;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Serilog;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace MQTTServer;

public class Server
{
    private readonly ServerInfo _serverInfo;
    private readonly MqttServerFactory _factory = new();
    private readonly X509Certificate _certificate;
    public readonly ConcurrentQueue<(string, MqttApplicationMessage)> RecvQueue = new();
    private object _locker = new object();
    public HashSet<string> Clients = new HashSet<string>();
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
            _server.ClientConnectedAsync += ServerOnClientConnectedAsync;
            _server.ClientDisconnectedAsync += ServerOnClientDisconnectedAsync;
            _server.ClientSubscribedTopicAsync += ServerOnClientSubscribedTopicAsync;
            _server.InterceptingPublishAsync += ServerOnInterceptingPublishAsync;
            await _server.StartAsync();
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
            Clients.Add(arg.ClientId);
        }

        return Task.CompletedTask;
    }

    private Task ServerOnClientSubscribedTopicAsync(ClientSubscribedTopicEventArgs arg)
    {
        //Log.Information($"Client {arg.ClientId} Subscribed {arg.TopicFilter.Topic}");
        return Task.CompletedTask;
    }

    private Task ServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs arg)
    {
        //Log.Information($"Client {arg.ClientId}({arg.Endpoint}) disconnected.");
        Clients.Remove(arg.ClientId);
        return Task.CompletedTask;
    }

    private Task ServerOnClientConnectedAsync(ClientConnectedEventArgs arg)
    {
        //Log.Information($"Client {arg.ClientId}({arg.Endpoint}) connected.");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_server.IsStarted)
            await _server.StopAsync();
    }

    private void AddClient(string clientId)
    {
        lock (_locker)
        {
            if (Clients.Contains(clientId)) return;
            Clients.Add(clientId);
        }
    }

    private void RemoveClient(string clientId)
    {
        lock (_locker)
        {
            Clients.Remove(clientId);
        }
    }
}

public class ServerInfo
{
    public int Port { get; set; } = 9090;
    public string UserName { get; set; } = "test";
    public string Password { get; set; } = "Pass99";
    public string CertificatePath { get; set; } = "server.crt";
}