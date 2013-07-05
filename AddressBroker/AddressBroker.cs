using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ZMQ;
using log4net;

namespace AddressBroker
{
    public class AddressBroker
    {
        private readonly AddressBrokerConfig _config;
        private readonly ILog _log;

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
            _log.Info("shutting down");
            _running = false;
            Thread.Sleep(_config.ReceiveMessageTimeoutInMilliseconds + 100);
            _context.Dispose();
        }

        private void CleanupHosts()
        {
            while (_running)
            {
                var deadHosts = _liveHosts.Where(h => h.Value.AddMilliseconds(_config.MaximumIntervalWithoutHeartbeatInMilliseconds) < DateTime.UtcNow).Select(h => h.Key).ToList();
                foreach (var deadHost in deadHosts)
                {
                    _log.WarnFormat("HOST {0} IS DEAD", deadHost);
                    _liveHosts.Remove(deadHost);
                }

                Thread.Sleep(_config.CleanupUnresponsiveHostsIntervalInMilliseconds);
            }
        }

        private void ListenForGetHosts()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = String.Format(_config.HostName, _config.GetHostsPort);
                socket.Bind(host);
                _log.Info("Listening for get-hosts messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode, _config.ReceiveMessageTimeoutInMilliseconds);
                    if (!String.IsNullOrEmpty(message))
                    {
                        socket.Send(SerializeHosts(_liveHosts.Keys), Encoding.Unicode);
                    }
                }
                _log.Info("get-hosts listener stopped");
            }
        }

        private void ListenForHeartbeats()
        {
            using (var socket = _context.Socket(SocketType.REP))
            {
                var host = String.Format(_config.HostName, _config.HeartbeatPort);
                socket.Bind(host);
                _log.Info("Listening for heartbeat messages on " + host);

                while (_running)
                {
                    var message = socket.Recv(Encoding.Unicode, _config.ReceiveMessageTimeoutInMilliseconds);
                    if (!String.IsNullOrEmpty(message))
                    {
                        AddOrUpdateHostInList(message);
                        socket.Send("ok", Encoding.Unicode);
                    }
                }
                _log.Info("heartbeat listener stopped");
            }
        }

        private void AddOrUpdateHostInList(string message)
        {
            if (!_liveHosts.ContainsKey(message))
            {
                //if not in the hosts list add new service to live hosts list
                _log.InfoFormat("New host {0} online", message);
                _liveHosts.Add(message, DateTime.UtcNow);
            }
            else
            {
                //if already in the list update the last-communication time
                _liveHosts[message] = DateTime.UtcNow;
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

        public AddressBroker(AddressBrokerConfig config, ILog log)
        {
            _config = config;
            _log = log;
        }

        public AddressBroker() : this(new AddressBrokerConfig(), LogManager.GetLogger(typeof(AddressBroker))) {}
    }
}
