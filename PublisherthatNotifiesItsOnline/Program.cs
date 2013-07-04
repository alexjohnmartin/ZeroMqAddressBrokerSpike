using System;
using System.Text;
using System.Threading;
using ZMQ;

namespace PublisherWithHeartbeat
{
    class Program
    {
        private const string HeartbeatPort = "6001";
        private const string PublishMessage = "parkingEvent ThisIsCoolRight?";
        private const string PublishPort = "6000";
        private const string HostName = "tcp://127.0.0.1:{0}";
        private const int HeartBeatIntervalInMilliseconds = 1000;
        private const int BrokerDeadIntervalInMilliseconds = 3000;
        private const int HeartBeatTimeoutInMilliseconds = 100;

        private static bool _running;
        private static bool _brokerAlive;
        private static Context _context;
        private static string _heartbeatHost;
        private static string _publishHost;

        static void Main(string[] args)
        {
            _running = true; 
            _heartbeatHost = string.Format(HostName, HeartbeatPort);
            _publishHost = string.Format(HostName, PublishPort);

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
                socket.Bind(_publishHost);

                while (_running)
                {
                    if (_brokerAlive)
                    {
                        Console.WriteLine("publishing message...");
                        socket.Send(PublishMessage, Encoding.Unicode);

                        Thread.Sleep(random.Next(100, HeartBeatIntervalInMilliseconds));
                    }
                    else
                    {
                        Thread.Sleep(BrokerDeadIntervalInMilliseconds);
                    }
                }
            }
        }

        private static void SendHeartbeat()
        {
            while (_running)
            {
                using (var socket = _context.Socket(SocketType.REQ))
                {
                    try
                    {
                        //Console.Write("sending heartbeat...");
                        socket.Connect(_heartbeatHost);
                        socket.Send(_publishHost, Encoding.Unicode);
                        var response = socket.Recv(Encoding.Unicode, HeartBeatTimeoutInMilliseconds);
                        //Console.WriteLine(" ...received repsonse - " + response);

                        if (!string.IsNullOrEmpty(response))
                        {
                            if (!_brokerAlive)
                            {
                                Console.WriteLine("address broker alive");
                                _brokerAlive = true;
                            }
                            
                            Thread.Sleep(HeartBeatIntervalInMilliseconds);
                        }
                        else
                        {
                            BrokerIsDeadPause();
                        }
                    }
                    catch (ZMQ.Exception exception)
                    {
                        Console.WriteLine("...heartbeat error: " + exception.Message);
                        BrokerIsDeadPause();
                    }
                }
            }
        }

        private static void BrokerIsDeadPause()
        {
            _brokerAlive = false;
            Console.WriteLine("Address broker probably unreachable - pausing");
            Thread.Sleep(BrokerDeadIntervalInMilliseconds);
        }
    }
}
