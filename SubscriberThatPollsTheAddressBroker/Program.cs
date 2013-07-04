using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ZMQ;

namespace SubscriberThatPollsTheAddressBroker
{
    class Program
    {
        private static bool _running;
        private static bool _brokerAlive;
        private static IList<string> _publisherAddresses;
        private static IList<Thread> _subscriberThreads;
        private static Context _context;
        private const string Filter = "parkingEvent ";
        private const string EmptyListMessage = "(empty)"; 
        private const string AddressBrokerAddress = "tcp://127.0.0.1:6002";
        private const int AddressBrokerPollingIntervalInMilliseconds = 1000;
        private const int BrokerDeadIntervalInMilliseconds = 3000;
        private const int ReceiveMessageTimeoutInMilliseconds = 100;
        private const char ListSeparator = '|'; 

        static void Main(string[] args)
        {
            _running = true; 
            _publisherAddresses = new List<string>();
            _subscriberThreads = new List<Thread>();

            using (_context = new Context(1))
            {
                var addressBrokerPollerThread = new Thread(PollAddressBroker);
                addressBrokerPollerThread.Start();

                Console.WriteLine("Subscriber started");
                Console.ReadLine();

                Console.WriteLine();
                Console.WriteLine("***** shutting down *****");
                _running = false;
                _publisherAddresses.Clear();
                Thread.Sleep(AddressBrokerPollingIntervalInMilliseconds + 500);
            }
        }

        private static void PollAddressBroker()
        {
            while (_running)
            {
                using (var socket = _context.Socket(SocketType.REQ))
                {
                    try
                    {
                        //Console.WriteLine("getting addresses from address broker");
                        socket.Connect(AddressBrokerAddress);
                        socket.Send("get", Encoding.Unicode);
                        var message = socket.Recv(Encoding.Unicode, ReceiveMessageTimeoutInMilliseconds);
                        if (!string.IsNullOrEmpty(message))
                        {
                            if (!_brokerAlive)
                            {
                                Console.WriteLine("Broker is alive");
                                _brokerAlive = true;
                            }

                            ParseListOfPublishers(message);
                            Thread.Sleep(AddressBrokerPollingIntervalInMilliseconds);
                        }
                        else
                        {
                            BrokerDeadPause();
                        }
                    }
                    catch (ZMQ.Exception exception)
                    {
                        Console.WriteLine("error - " + exception.Message);
                        BrokerDeadPause();
                    }
                }
            }
            Console.WriteLine("Terminating poll address broker thread - ");
        }

        private static void BrokerDeadPause()
        {
            _brokerAlive = false; 
            Console.WriteLine("Address broker is probably dead/unreachable");
            Thread.Sleep(BrokerDeadIntervalInMilliseconds);
        }

        private static void ParseListOfPublishers(string stringListOfPublishers)
        {
            var latestPublisherAddresses = stringListOfPublishers != EmptyListMessage ? stringListOfPublishers.Split(ListSeparator) : new string[0];

            //add new publishers 
            foreach (var address in latestPublisherAddresses.Where(address => !_publisherAddresses.Contains(address)))
            {
                Console.WriteLine("new publisher - " + address);
                int index = _publisherAddresses.Count; 
                _publisherAddresses.Add(address);
                _subscriberThreads.Add(new Thread(SubscribeToPublisherMessages));
                _subscriberThreads[index].Start(address);
            }

            //remove dead publishers
            var deadAddresses = new List<string>(_publisherAddresses.Where(address => !latestPublisherAddresses.Contains(address))); 
            foreach (var address in deadAddresses)
            {
                Console.WriteLine("dead publisher - " + address);
                int index = _publisherAddresses.IndexOf(address);
                _publisherAddresses.RemoveAt(index); 
                Thread.Sleep(ReceiveMessageTimeoutInMilliseconds + 100);
                _subscriberThreads.RemoveAt(index);
            }
        }

        private static void SubscribeToPublisherMessages(object param)
        {
            var address = param.ToString();
            using (var socket = _context.Socket(SocketType.SUB))
            {
                socket.Connect(address);
                socket.Subscribe(Filter, Encoding.Unicode);
                while (_running && _publisherAddresses.Contains(address))
                {
                    var message = socket.Recv(Encoding.Unicode, ReceiveMessageTimeoutInMilliseconds); 
                    if (!string.IsNullOrEmpty(message))
                        Console.WriteLine("published message received = " + message);
                }
            }
            Console.WriteLine("Terminating subscribe thread - " + address);
        }
    }
}
