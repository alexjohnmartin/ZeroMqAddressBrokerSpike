using System;
using System.Text;
using System.Threading;
using ZMQ;

namespace PublisherWithHeartbeat
{
    class Program
    {
        private static Guid _processGuid;
        private static bool _running = true; 
        private const string HeartbeatMessage = "notifyOnline UUID={0}";
        private const string HeartbeatPort = "6001";
        private const string HostName = "tcp://127.0.0.1:{0}";
        private const int HeartBeatIntervalInMilliseconds = 5000; 

        static void Main(string[] args)
        {
            _processGuid = Guid.NewGuid();

            var heartbeatThread = new Thread(SendHeartbeat); 
            heartbeatThread.Start();

            Console.WriteLine("running...");
            Console.WriteLine("(press RETURN to stop)");
            Console.ReadLine();

            Console.WriteLine("shutting down...");
            _running = false; 
            Thread.Sleep(HeartBeatIntervalInMilliseconds + 100);
        }

        private static void SendHeartbeat()
        {
            using (var context = new Context(1))
            {
                using (var socket = context.Socket(SocketType.REQ))
                {
                    var host = string.Format(HostName, HeartbeatPort);
                    socket.Connect(host);

                    while (_running)
                    {
                        Console.Write("sending notification...");
                        socket.Send(string.Format(HeartbeatMessage, _processGuid), Encoding.Unicode);
                    
                        var response = socket.Recv(Encoding.Unicode); 
                        Console.WriteLine(" ...received repsonse - " + response);
                        Thread.Sleep(HeartBeatIntervalInMilliseconds);
                    }
                }
            }
        }
    }
}
