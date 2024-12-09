using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Common;
using MQTTnet;
using MQTTnet.Formatter;
using Serilog;

namespace MQTTClient;

/// <summary>
/// Dummy - MQTT
/// </summary>
public class Client
{
    private readonly MqttClientFactory _factory;
    private readonly MqttClientOptionsBuilder _builder;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly ClientInfo _clientInfo;
    private string Id => _clientInfo.Id;
    private readonly string _sendTopic;
    private readonly string _receiveTopic;
    private DateTime LastRecv = DateTime.MinValue;
    private DateTime LastSend = DateTime.MinValue;
    private DateTime LastOnline = DateTime.MinValue;
    private DateTime LastOffline = DateTime.MinValue;
    private ClientStatus Status = ClientStatus.Init;
    private int TryCount = 0;
    private int OfflineCount = 0;
    private readonly ConcurrentQueue<ArraySegment<byte>> SendQueue = new();
    private readonly ConcurrentQueue<ArraySegment<byte>> RecvQueue = new();
    private readonly Action<string, bool> OnStatus;
    private readonly Action<string, ReadOnlySequence<byte>> OnData;
    private int Token = 0;
    private TaskData Data;

    /// <summary>
    /// Need report
    /// </summary>
    public bool NeedReport => (DateTime.Now - LastSend).TotalSeconds > 10;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="info">eStation information</param>
    /// <param name="onData">Action onStatus</param>
    /// <param name="onStatus">Action onStatus</param>
    public Client(ClientInfo info, Action<string, bool> onStatus, Action<string, ReadOnlySequence<byte>> onData)
    {
        _clientInfo = info;
        _sendTopic = $"/estation/{info.Id}/send";
        _receiveTopic = $"/estation/{info.Id}/receive";
        _factory = new MqttClientFactory();
        _builder = new MqttClientOptionsBuilder()
            .WithClientId(info.Id)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCredentials(_clientInfo.UserName, _clientInfo.Password)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(Math.Max(15, info.Heartbeat)));

        var i = _clientInfo.Server.IndexOf(':');
        _builder = i == -1
            ? _builder.WithTcpServer(_clientInfo.Server)
            : _builder.WithTcpServer(_clientInfo.Server[..i], int.Parse(_clientInfo.Server[(i + 1)..]));
        _options = _builder.Build();
        _client = _factory.CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += Instance_ReceivedAsync;
        _client.ConnectedAsync += Instance_ConnectedAsync;
        _client.DisconnectedAsync += Instance_DisconnectedAsync;

        OnStatus = onStatus;
        OnData = onData;
        Data = new TaskData();
    }

    /// <summary>
    /// Run MQTT client
    /// </summary>
    public void Run()
    {
        // Thread to keep connection
        Task.Run(async () =>
        {
            Exception pre = new();
            var errorCount = 0;
            while (true)
            {
                try
                {
                    if (_client.IsConnected && Status == ClientStatus.Connect)
                    {
                        TryCount = 0;
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        continue;
                    }

                    if (TryCount++ == 0) Log.Information($"[Dummy]Try connect to:{_clientInfo.Server}");
                    if (TryCount >= 0xFF) TryCount = 0; // Prevent too much try logs
                    var result = await _client.ConnectAsync(_options);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    if (ex.HResult != pre.HResult)
                    {
                        pre = ex;
                        errorCount = 0;
                        Log.Error(ex.HResult + ":" + ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                    else if (errorCount > 0xFFFF)
                    {
                        // Loop Error
                        errorCount = 0;
                        Log.Error(ex.HResult + ":" + ex.Message);
                    }
                    else
                    {
                        errorCount++;
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
            }
        });
    }

    /// <summary>
    /// Test
    /// </summary>
    public async Task Test()
    {
        Data.Date = DateTime.Now;
        Data.Data = Guid.NewGuid().ToByteArray();
        Data.TaskId++;
        await SendData(Data, MessageCode.Data);
    }

    /// <summary>
    /// Check
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private bool Check(TaskResult data)
    {
        return data.TaskId == Data.TaskId && data.Data.Reverse().SequenceEqual(data.Data);
    }

    /// <summary>
    /// Send data
    /// </summary>
    /// <param name="t">Data to send</param>
    /// <param name="code">Client code</param>
    /// <param name="oneTime">Payload</param>
    /// <returns>The task</returns>
    public async Task SendData<T>(T t, MessageCode code, bool oneTime = true)
    {
        if (Status != ClientStatus.Connect)
        {
            if (!oneTime)
                SendQueue.Enqueue(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(t))));
            return;
        }

        try
        {
            var message = new MqttApplicationMessage
            {
                Topic = _sendTopic,
                PayloadSegment = JsonSerializer.SerializeToUtf8Bytes(new Message(code, Token++, t)),
                QualityOfServiceLevel = oneTime
                    ? MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce
                    : MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
            };

            var result = await _client.PublishAsync(message);
            LastSend = DateTime.Now;
            Log.Information($"[Dummy]Send data to:{_sendTopic},{code}. Result: {result}");
        }
        catch (Exception ex)
        {
            Log.Error("SEND_ERR:" + ex.Message);
        }
    }

    /// <summary>
    /// Message received event handler
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private Task Instance_ReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        try
        {
            if (arg.ApplicationMessage.Payload.Length > 0)
            {
                OnData.Invoke(_clientInfo.Id, arg.ApplicationMessage.Payload);
                LastRecv = DateTime.Now;
            }

            var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(arg.ApplicationMessage.Payload));
            if (message == null) return Task.CompletedTask;
            switch (message.Code)
            {
                case MessageCode.Data:
                case MessageCode.Result:
                    if (message.Body is not TaskResult task) return Task.CompletedTask;
                    Log.Information(
                        $"Client {Id} received {arg.ApplicationMessage.Payload.Length} bytes, Check: {Check(task)}.");
                    break;
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RECV_ERROR");
            throw;
        }
    }

    /// <summary>
    /// Connected event handler
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    private async Task Instance_ConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        var subscript = _factory
            .CreateSubscribeOptionsBuilder()
            .WithTopicFilter(x => x.WithTopic(_receiveTopic))
            .Build();
        await _client.SubscribeAsync(subscript);
        LastOnline = DateTime.Now;
        Status = ClientStatus.Connect;
        OnStatus.Invoke(_clientInfo.Id, true);
        Log.Information($"Connect to {_clientInfo.Server} OK");
    }

    /// <summary>
    /// Disconnected event handler
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    private Task Instance_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        if (Status != ClientStatus.Disconnect) Log.Warning("Disconnect."); // Record first time

        OfflineCount++;
        LastOffline = DateTime.Now;
        Status = ClientStatus.Disconnect;
        OnStatus.Invoke(_clientInfo.Id, false);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Client information
/// </summary>
public class ClientInfo
{
    public string Id { get; set; }
    public int Heartbeat { get; set; } = 15;
    public string Server { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="id">Client ID</param>
    /// <param name="server">Server address</param>
    /// <param name="userName">User name</param>
    /// <param name="password">Password</param>
    public ClientInfo(string id, string server, string userName, string password)
    {
        Id = id;
        Server = server;
        UserName = userName;
        Password = password;
    }
}

public enum ClientStatus
{
    Init,
    Connect,
    Disconnect
}