using System;
using System.Linq;
using Topshelf;

namespace ServiceFabricMonitorService
{
    class Program
    {
        static void Main(string[] args)
        {
            //Command line options: https://topshelf.readthedocs.io/en/latest/overview/commandline.html

            //Note: The service account used to run this app needs:
            //a) local machine admin rights for perf counters & ETW event subscription
            //b) Permission to send network traffic to Azure App Insights (this code doesn't support proxies, but you can certainly add it, using default .NET proxy config mechanisms)

            //ServiceFabricMonitorService.exe help
            //ServiceFabricMonitorService.exe uninstall
            //ServiceFabricMonitorService.exe install -username "DOMAIN\Service Account" -password "Its A Secret"

            const string serviceName = "ServiceFabric-Node-Monitor";

            var host = HostFactory.New(x =>
            {
                x.DependsOnEventLog();
                
                x.EnableShutdown();

                x.SetServiceName(serviceName);
                x.SetDisplayName($"ServiceFabric Node Monitor for {Environment.MachineName}");
                x.SetDescription(
                    "Monitors Important Service Fabric Cluster/Node Events (Events, Performance Counters, etc)");
                x.StartAutomaticallyDelayed();
                 
                x.Service<MonitoringService>(s =>
                    {
                        s.ConstructUsing(() => new MonitoringService(serviceName))
                            .WhenStarted(m => m.Start())
                            .WhenPaused(m => m.Stop())
                            .WhenContinued(m => m.Start())
                            .WhenStopped(m => m.Stop())
                            .WhenShutdown(m => m.Stop());
                    }
                );
                x.RunAsLocalSystem();
            });

            var exitCode = host.Run();

            var serviceExitCode = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = serviceExitCode;
        }
    }
}
