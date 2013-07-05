using Topshelf;
using log4net.Config;

namespace AddressBroker
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            HostFactory.Run(x =>
                {
                    x.Service<AddressBroker>(s =>
                        {
                            s.ConstructUsing(ab => new AddressBroker());
                            s.WhenStarted(ab => ab.Run());
                            s.WhenStopped(ab => ab.Stop());
                            s.WhenShutdown(ab => ab.Stop());
                        });
                    x.RunAsLocalService();
                    x.SetDescription("Pbp Address Broker Service");
                    x.SetDisplayName("Pbp Address Broker Service");
                    x.SetServiceName("PbpAddressBrokerService");  
                });
        }
    }
}
