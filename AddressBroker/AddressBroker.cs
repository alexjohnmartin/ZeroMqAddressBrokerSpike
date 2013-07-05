using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ZMQ;

namespace AddressBroker
{
    public class AddressBroker
    {
        private readonly AddressBrokerConfig _config;

        public void Start()
        {
            _liveHosts = new Dictionary<string, DateTime>();
            _context = new Context(1);
            
            var heartbeatListenerThread = new Thread(ListenForHeartbeats);
            var getHostsListenerThread = new Thread(ListenForGetHosts);
            var cleanupHostsThread = new Thread(CleanupHosts);

            heartbeatListenerThread.Start();
            getHostsListenerThread.Start();
            cleanupHostsThread.Start();
        }

        public void Stop()
        {
            Console.WriteLine();
            Console.WriteLine("***** shutting down *****");
            _running = false;
            SendShutdownMessages();
            Thread.Sleep(500);
            _context.Dispose();
        }

        private void CleanupHosts()
        {
            while (_running)
            {
                var deadHosts = _liveHosts.Where(h => h.Value.AddMilliseconds(_config.MaximumIntervalWithoutHeartbeatInMilliseconds) < DateTime.UtcNow).Select(h => h.Key).ToList();
                foreach (var deadHost in deadHosts)
                {
                    Console.WriteLine();
                    Console.WriteLine("*********** HOST {0} IS DEAD *************", deadHost);
                    _liveHosts.Remove(deadHost);
                }

                Thread.Sleep(_config.CleanupUnresponsiveHostsIntervalInMilliseconds);
            }
        }

        private void SendShutdownMessages()
        {
            using (var context = new Context(1))
            {
                using (var socket = context.Socket(SocketType.REQ))
                {
                    var host = String.Format(_config.HostName, _config.GetHostsPort);
                    socket.Connect(host);
                    socket.Send(AbortMessage, Encoding.Unicode);
                    socket.Recv(Encoding.Unicode);
                }

                using (var socket = context.Socket(SocketType.REQ))
                {
                    var host = String.Format(_config.HostName, _config.HeartbeatPort);
                    socket.Connect(host);
                    socket.Send(AbortMessage, Encoding.Unicode);
                    socket.Recv(Encoding.Unicode);
                }
            }
        }

        private void ListenForGetHosts()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = String.Format(_config.HostName, _config.GetHostsPort);
                socket.Bind(host);
                Console.WriteLine("Listening for get-hosts messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode, _config.ReceiveMessageTimeoutInMilliseconds);
                    if (!String.IsNullOrEmpty(message))
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

        private void ListenForHeartbeats()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = String.Format(_config.HostName, _config.HeartbeatPort);
                socket.Bind(host);
                Console.WriteLine("Listening for heartbeat messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode, _config.ReceiveMessageTimeoutInMilliseconds);
                    if (!String.IsNullOrEmpty(message))
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

        private string SerializeHosts(IEnumerable<string> liveHosts)
        {
            var enumerable = liveHosts as IList<string> ?? liveHosts.ToList();
            return enumerable.Any() ? enumerable.Aggregate((current, next) => current + _config.ListSeparator + next) : _config.EmptyListMessage;
        }

        private bool _running = true;
        private IDictionary<string, DateTime> _liveHosts;
        private Context _context;
        private const string AbortMessage = "abort";

        public AddressBroker(AddressBrokerConfig config)
        {
            _config = config;
        }

        public AddressBroker() : this(new AddressBrokerConfig()) {}
    }
}
