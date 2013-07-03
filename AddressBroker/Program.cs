using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ZMQ;

namespace AddressBroker
{
    class Program
    {
        private static bool _running = true; 
        private static List<Host> _liveHosts;
        private static Context _context;
        private const string HeartbeatPort = "6001";
        private const string GetHostsPort = "6002";
        private const string HostName = "tcp://127.0.0.1:{0}";
        private const string AbortMessage = "abort";

        static void Main(string[] args)
        {
            _liveHosts = new List<Host>();

            using (_context = new Context(1))
            {
                var notifyListenerThread = new Thread(ListenForNotifications);
                var getHostsListenerThread = new Thread(ListenForGetHosts);

                notifyListenerThread.Start();
                getHostsListenerThread.Start();

                Console.ReadLine();

                //shutdown threads
                Console.WriteLine();
                Console.WriteLine("***** shutting down *****");
                _running = false;
                SendShutdownMessages(); 
                Thread.Sleep(500);

                notifyListenerThread.Abort();
                getHostsListenerThread.Abort();
            }

            Console.WriteLine();
            Console.WriteLine("***** stopped - press RETURN to exit *****");
            Console.ReadLine();
        }

        private static void SendShutdownMessages()
        {
            using (var context = new Context(1))
            {
                using (var socket = context.Socket(SocketType.REQ))
                {
                    var host = string.Format(HostName, GetHostsPort);
                    socket.Connect(host);
                    socket.Send(AbortMessage, Encoding.Unicode);
                }

                using (var socket = context.Socket(SocketType.REQ))
                {
                    var host = string.Format(HostName, HeartbeatPort);
                    socket.Connect(host);
                    socket.Send(AbortMessage, Encoding.Unicode);
                }
            }
        }

        private static void ListenForGetHosts()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = string.Format(HostName, GetHostsPort);
                socket.Bind(host);
                Console.WriteLine("Listening for get-hosts messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode);
                    Console.WriteLine();
                    Console.WriteLine("Received get-hosts message: " + message);

                    if (message != AbortMessage)
                    {
                        //TODO:remove any hosts that have exceeded the heartbeat timeout

                        //TODO:return list of live hosts
                        //socket.Send(SerializeHosts(_liveHosts), Encoding.Unicode);
                    }
                }
            }
        }

        private static void ListenForNotifications()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = string.Format(HostName, HeartbeatPort); 
                socket.Bind(host);
                Console.WriteLine("Listening for heartbeat messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode);
                    Console.WriteLine();
                    Console.WriteLine("Received heartbeat message: " + message);

                    if (message != AbortMessage)
                    {
                        //TODO:if not in the hosts list add new service to live hosts list
                        //TODO:if already in the list update the last-communication time
                    }
                }
            }
        }

        private static string SerializeHosts(List<Host> liveHosts)
        {
            return "test";
        }
    }
}
