using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ZMQ;

namespace AddressBroker
{
    class Program
    {
        private static bool _running = true; 
        private static IDictionary<string, DateTime> _liveHosts;
        private static Context _context;
        private const string HeartbeatPort = "6001";
        private const string GetHostsPort = "6002";
        private const string HostName = "tcp://127.0.0.1:{0}";
        private const string AbortMessage = "abort";
        private const string EmptyListMessage = "(empty)"; 
        private const char ListSeparator = '|'; 
        private const int MaximumIntervalWithoutHeartbeatInMilliseconds = 2000;
        private const int CleanupUnresponsiveHostsIntervalInMilliseconds = 100;
        private const int ReceiveMessageTimeoutInMilliseconds = 100;

        static void Main(string[] args)
        {
            _liveHosts = new Dictionary<string, DateTime>();

            using (_context = new Context(1))
            {
                var heartbeatListenerThread = new Thread(ListenForHeartbeats);
                var getHostsListenerThread = new Thread(ListenForGetHosts);
                var cleanupHostsThread = new Thread(CleanupHosts); 

                heartbeatListenerThread.Start();
                getHostsListenerThread.Start();
                cleanupHostsThread.Start();

                Console.WriteLine("(press RETURN to stop)");
                Console.ReadLine();

                //shutdown threads
                Console.WriteLine();
                Console.WriteLine("***** shutting down *****");
                _running = false;
                SendShutdownMessages(); 
                Thread.Sleep(500);
            }
        }

        private static void CleanupHosts()
        {
            while (_running)
            {
                var deadHosts = _liveHosts.Where(h => h.Value.AddMilliseconds(MaximumIntervalWithoutHeartbeatInMilliseconds) < DateTime.UtcNow).Select(h => h.Key).ToList();
                foreach (var deadHost in deadHosts)
                {
                    Console.WriteLine();
                    Console.WriteLine("*********** HOST {0} IS DEAD *************", deadHost);
                    _liveHosts.Remove(deadHost);
                }

                Thread.Sleep(CleanupUnresponsiveHostsIntervalInMilliseconds);
            }
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
                    socket.Recv(Encoding.Unicode); 
                }

                using (var socket = context.Socket(SocketType.REQ))
                {
                    var host = string.Format(HostName, HeartbeatPort);
                    socket.Connect(host);
                    socket.Send(AbortMessage, Encoding.Unicode);
                    socket.Recv(Encoding.Unicode); 
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
                    var message = socket.Recv(Encoding.Unicode, ReceiveMessageTimeoutInMilliseconds);
                    if (!string.IsNullOrEmpty(message))
                    {
                        //Console.WriteLine();
                        //Console.WriteLine("Received get-hosts message: " + message);
                    
                        if (message != AbortMessage)
                        {
                            socket.Send(SerializeHosts(_liveHosts.Keys), Encoding.Unicode);
                        }
                        else
                        {
                            socket.Send("aborted", Encoding.Unicode);
                            Console.WriteLine("get-hosts listener stopped");
                        }
                    }
                }
            }
        }

        private static void ListenForHeartbeats()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = string.Format(HostName, HeartbeatPort); 
                socket.Bind(host);
                Console.WriteLine("Listening for heartbeat messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode, ReceiveMessageTimeoutInMilliseconds);
                    if (!string.IsNullOrEmpty(message))
                    {
                        //Console.WriteLine();
                        //Console.WriteLine("Received heartbeat message: " + message);

                        if (message != AbortMessage)
                        {
                            if (!_liveHosts.ContainsKey(message))
                            {
                                //if not in the hosts list add new service to live hosts list
                                Console.WriteLine();
                                Console.WriteLine("New host {0} online", message);
                                _liveHosts.Add(message, DateTime.UtcNow);
                            }
                            else
                            {
                                //if already in the list update the last-communication time
                                _liveHosts[message] = DateTime.UtcNow;
                            }

                            socket.Send("ok", Encoding.Unicode);
                        }
                        else
                        {
                            socket.Send("aborted", Encoding.Unicode);
                            Console.WriteLine("heartbeat listener stopped");
                        }
                    }
                }
            }
        }

        private static string SerializeHosts(IEnumerable<string> liveHosts)
        {
            if (liveHosts.Any())
                return liveHosts.Aggregate((current, next) => current + ListSeparator + next);

            return EmptyListMessage; 
        }
    }
}
