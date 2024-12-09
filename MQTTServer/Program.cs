using System;

namespace MQTTServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var server = new Server(new ServerInfo());

            Task.Run(async () => { await server.Run(); });
            
            Console.WriteLine("Press any key to exit");
        }
    }
}