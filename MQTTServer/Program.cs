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
        PORT:
            Console.Write("Sever Port:");
            var input = Console.ReadLine();

            if (!int.TryParse(input, out var port) || port < 1000 || port > 65535)
            {
                Console.WriteLine("Invalid Port:" + input);
                goto PORT;
            }

            var server = new Server(new ServerInfo() { Port = port });
            long RecvCount = 0;
            long SendCount = 0;

            Task.Run(server.Run);

            Task.Run(async () =>
            {
                while (true)
                {
                    if (server.RecvQueue.IsEmpty)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.1));
                        continue;
                    }

                    while (server.RecvQueue.TryDequeue(out var data))
                    {
                        if (data.Item2.Topic != "/estation/send") continue;
                        var str = Encoding.UTF8.GetString(data.Item2.Payload);
                        var message = JsonSerializer.Deserialize<Message>(str);
                        if (message == null) continue;
                        Interlocked.Increment(ref RecvCount);
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
                                await server.SendMessage(data.Item1, result);
                                Interlocked.Increment(ref SendCount);
                                break;
                        }
                    }
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]Online:{server.Clients.Count}, Recv:{RecvCount}, Send:{SendCount}.");
                }
            });

            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }
    }
}