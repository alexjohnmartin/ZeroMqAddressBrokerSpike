using System.Configuration;

namespace AddressBroker
{
    public class AddressBrokerConfig
    {
        public string HeartbeatPort { get { return ConfigurationManager.AppSettings["HeartbeatPort"]; } }
        public string GetHostsPort { get { return ConfigurationManager.AppSettings["GetHostsPort"]; } }
        public string HostName { get { return ConfigurationManager.AppSettings["HostName"]; } }
        public string EmptyListMessage { get { return ConfigurationManager.AppSettings["EmptyListMessage"]; } }
        public char ListSeparator { get { return ConfigurationManager.AppSettings["ListSeparator"][0]; } }
        public int MaximumIntervalWithoutHeartbeatInMilliseconds { get { return int.Parse(ConfigurationManager.AppSettings["MaximumIntervalWithoutHeartbeatInMilliseconds"]); } }
        public int CleanupUnresponsiveHostsIntervalInMilliseconds { get { return int.Parse(ConfigurationManager.AppSettings["CleanupUnresponsiveHostsIntervalInMilliseconds"]); } }
        public int ReceiveMessageTimeoutInMilliseconds { get { return int.Parse(ConfigurationManager.AppSettings["ReceiveMessageTimeoutInMilliseconds"]); } }
    }
}