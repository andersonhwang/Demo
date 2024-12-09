using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Common;
using MQTTnet;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Serilog;

namespace MQTTServer;

public class Server
{
    private readonly ServerInfo _serverInfo;
    private readonly MqttServerFactory _factory = new();
    private readonly X509Certificate2 _certificate;
    private readonly ConcurrentQueue<InterceptingPublishEventArgs> _publishQueue = new();
    private MqttServer _server;

    public Server(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
        _certificate = X509Certificate2.CreateFromPem(_serverInfo.CertificatePath);
    }

    public async Task Run()
    {
        var options = new MqttServerOptionsBuilder()
            .WithEncryptionCertificate(_certificate)
            .WithEncryptedEndpoint()
            .Build();
        _server = _factory.CreateMqttServer(options);
        _server.ValidatingConnectionAsync += ServerOnValidatingConnectionAsync;
        _server.ClientConnectedAsync += ServerOnClientConnectedAsync;
        _server.ClientDisconnectedAsync += ServerOnClientDisconnectedAsync;
        _server.ClientSubscribedTopicAsync += ServerOnClientSubscribedTopicAsync;
        _server.InterceptingPublishAsync += ServerOnInterceptingPublishAsync;
        await _server.StartAsync();
        
        
    }

    private Task ServerOnInterceptingPublishAsync(InterceptingPublishEventArgs arg)
    {
        _publishQueue.Enqueue(arg);
        return Task.CompletedTask;
    }

    public async Task SendMessage(string clientId, Message message)
    {
        // Create a new message using the builder as usual.
        var mqtt = new MqttApplicationMessageBuilder()
            .WithTopic($"/estation/{clientId}/recv")
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
        return Task.CompletedTask;
    }

    private Task ServerOnClientSubscribedTopicAsync(ClientSubscribedTopicEventArgs arg)
    {
        Log.Information($"Client {arg.ClientId} Subscribed {arg.TopicFilter.Topic}");
        return Task.CompletedTask;
    }

    private Task ServerOnClientDisconnectedAsync(ClientDisconnectedEventArgs arg)
    {
        Log.Information($"Client {arg.ClientId}({arg.Endpoint}) disconnected.");
        return Task.CompletedTask;
    }

    private Task ServerOnClientConnectedAsync(ClientConnectedEventArgs arg)
    {
        Log.Information($"Client {arg.ClientId}({arg.Endpoint}) connected.");
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
    public string CertificatePath { get; set; } = "server.pem";
}