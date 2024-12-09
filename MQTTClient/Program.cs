using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Common;
using Serilog;

namespace MQTTClient
{
    internal class Program
    {
        readonly static string _serverAddress = "192.168.0.1";
        readonly static string _userName = "test";
        readonly static string _password = "123456";
        
        static void Main(string[] args)
        {
            var clients = new List<Client>();
            CLIENT_COUNT:
            Console.WriteLine("Enter client count:");
            var count = Console.ReadLine();
            if (!int.TryParse(count, out var mqttClient))
            {
                Console.WriteLine("Please enter a valid number:");
                goto CLIENT_COUNT;
            };
            for (var i = 0; i < mqttClient; i++)
            {
                clients.Add(
                    new Client(new ClientInfo((i + 1).ToString(), _serverAddress, _userName, _password),
                    OnStatus, OnData));
            }

            foreach (var client in clients)
            {
                client.Run();
            }

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    foreach (var client in clients)
                    {
                        await client.Test();
                    }
                }
            });
            Console.ReadLine();Console.ReadLine();Console.ReadLine();
        }

        /// <summary>
        /// On-status
        /// </summary>
        /// <param name="id">Client ID</param>
        /// <param name="status">Client status</param>
        static void OnStatus(string id, bool status)
        {
            if (status)
            {
                Log.Information($"Client {id} connected.");
            }
            else
            {
                Log.Error($"Client {id} disconnected.");   
            }
        }

        /// <summary>
        /// On-data
        /// </summary>
        /// <param name="id">Client ID</param>
        /// <param name="data">Client data</param>
        static void OnData(string id, ReadOnlySequence<byte> data)
        {
            // TODO
        }
    }
}