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
        private static bool _brokerAlive = true;
        private static Context _context;
        private const string HeartbeatPort = "6001";
        private const string PublishMessage = "parkingEvent ThisIsCoolRight?";
        private const string PublishPort = "6000";
        private const string HostName = "tcp://127.0.0.1:{0}";
        private const int HeartBeatIntervalInMilliseconds = 3000;
        private const int BrokerDeadIntervalInMilliseconds = 10000; 

        static void Main(string[] args)
        {
            _processGuid = Guid.NewGuid();

            using (_context = new Context(1))
            {
                var heartbeatThread = new Thread(SendHeartbeat);
                heartbeatThread.Start();

                var publishThread = new Thread(PublishMessages);
                publishThread.Start();

                Console.WriteLine("running...");
                Console.WriteLine("(press RETURN to stop)");
                Console.ReadLine();

                Console.WriteLine("shutting down...");
                _running = false;
                Thread.Sleep(HeartBeatIntervalInMilliseconds + 100);
            }
        }

        private static void PublishMessages()
        {
            var random = new Random(); 
            using (var socket = _context.Socket(SocketType.PUB))
            {
                var host = string.Format(HostName, PublishPort);
                socket.Connect(host);

                while (_running && _brokerAlive)
                {
                    Console.Write("publishing message...");
                    socket.Send(PublishMessage, Encoding.Unicode);

                    Thread.Sleep(random.Next(100, HeartBeatIntervalInMilliseconds));
                }
            }
        }

        private static void SendHeartbeat()
        {
            var heartbeatHost = string.Format(HostName, HeartbeatPort);
            var publishHost = string.Format(HostName, PublishPort);

            using (var socket = _context.Socket(SocketType.REQ))
            {
                socket.Connect(heartbeatHost);

                while (_running)
                {
                    Console.Write("sending heartbeat...");

                    try
                    {
                        socket.Send(publishHost, Encoding.Unicode);
                        var response = socket.Recv(Encoding.Unicode, 100);
                        Console.WriteLine(" ...received repsonse - " + response);

                        _brokerAlive = true; 
                        Thread.Sleep(HeartBeatIntervalInMilliseconds);
                    }
                    catch (System.Exception exception)
                    {
                        Console.WriteLine("...error: " + exception.Message);
                        Console.WriteLine("Address broker probably unreachable - pausing");

                        _brokerAlive = false; 
                        Thread.Sleep(BrokerDeadIntervalInMilliseconds);
                    }
                }
            }
        }
    }
}
