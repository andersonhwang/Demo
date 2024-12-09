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
                        var str = Encoding.UTF8.GetString(data.ApplicationMessage.Payload);
                        var message = JsonSerializer.Deserialize<Message>(str);
                        if (message == null) continue;
                        switch (message.Code)
                        {
                            case MessageCode.Data:
                                var task = JsonSerializer.Deserialize<TaskData>(str);
                                if (task is null) continue;
                                var result = new TaskResult(MessageCode.Result, message.Token)
                                {
                                    Date = DateTime.Now,
                                    TaskId = task.TaskId,
                                    Data = task.Data.Reverse().ToArray()
                                };
                                await server.SendMessage(data.ClientId, result);
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