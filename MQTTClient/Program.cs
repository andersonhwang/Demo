using System.Buffers;
using System.Collections.Concurrent;
using Serilog;

namespace MQTTClient
{
    internal class Program
    {
        private const string ServerAddress = "ap.etgtag.com";
        private const int ServerPort = 9072;
        private const string UserName = "test";
        private const string Password = "Pass99";
        private const int Limit = 1;
        private static long DisconnectCount = 0;
        private static long SendCount = 0;
        private static long ReceiveCount = 0;
        private static readonly ConcurrentQueue<(string, ReadOnlySequence<byte>)> RecvQueue = new();

        static void Main(string[] args)
        {
            // Prepare clients
            var clients = new List<Client>();
            for (var i = 0; i < Limit; i++)
            {
                clients.Add(
                    new Client(new ClientInfo((i + 1).ToString(), ServerAddress, ServerPort, UserName, Password),
                        OnStatus, OnData));
            }
            // Run clients
            foreach (var client in clients)
            {
                client.Run();
            }
            // Display
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}]Total:{Limit},Online:{clients.Count(x => x.IsConnected)},Disconnect:{DisconnectCount},Send:{SendCount},Receive:{ReceiveCount}.");
                }
            });
            Console.ReadLine();
            // Send messages
            Task.Run(async () =>
            {
                //await Task.Delay(120000);
                while (true)
                {
                    foreach (var client in clients)
                    {
                        await client.Test();
                        SendCount++;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
            });
            // Receive messages
            Task.Run(async () =>
            {
                while (true)
                {
                    if (RecvQueue.TryDequeue(out var queue))
                    {
                        ReceiveCount++;
                        continue;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            });
            Console.ReadLine();
            Console.ReadLine();
            Console.ReadLine();
        }

        /// <summary>
        /// On-status
        /// </summary>
        /// <param name="id">Client ID</param>
        /// <param name="status">Client status</param>
        static void OnStatus(string id, bool status)
        {
            // TODO
            if(!status) Interlocked.Increment(ref DisconnectCount);
        }

        /// <summary>
        /// On-data
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <param name="data">Client data</param>
        static void OnData(string clientId, ReadOnlySequence<byte> data)
        {
            RecvQueue.Enqueue(new(clientId, data));
        }
    }
}