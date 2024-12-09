using Common;
using Serilog;
using System;
using System.Text;
using System.Text.Json;

namespace MQTTServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Log, static configure
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("/root/logs/.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:HH:mm:ss.fff}[{Level:u1}]{Message} {NewLine}{Exception}",
                retainedFileCountLimit: 15)
                .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss.fff}[{Level:u1}]{Message} {NewLine}{Exception}")
                .CreateLogger();

            var server = new Server(new ServerInfo());

            Task.Run(server.Run);

            Task.Run(async () =>
            {
                while (true)
                {
                    if (server.RecvQueue.Count == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    while (server.RecvQueue.TryDequeue(out var data))
                    {
                        var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(data.ApplicationMessage.Payload));
                        if (message == null) continue;
                        switch (message.Code)
                        {
                            case MessageCode.Data:
                                var task = message.Body as TaskData;
                                if (task is null) continue;
                                await server.SendMessage(data.ClientId, new Message(MessageCode.Result, message.Token, new TaskResult
                                {
                                    Date = DateTime.Now,
                                    TaskId = task.TaskId,
                                    Data = task.Data.Reverse().ToArray()
                                }));
                                break;
                        }
                    }
                }
            });
            
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }
    }
}