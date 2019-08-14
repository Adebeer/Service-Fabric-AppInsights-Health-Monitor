using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ServiceFabricMonitorService
{
    public sealed class MonitoringService
    {
        public TraceEventSession EtwSession { get; private set; }
        public TelemetryConfiguration AppInsightsConfig { get; private set; }
        public TelemetryClient AppInsightsClient { get; private set; }
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

        public string SessionName { get; }

        public MonitoringService(string sessionName)
        {
            SessionName = sessionName;
        }

        public void Start()
        {
            if (!(TraceEventSession.IsElevated() ?? false))
            {
                throw new SecurityException("Admin rights required to turn on ETW event monitoring");
            }

            //https://docs.microsoft.com/en-us/azure/azure-monitor/app/console
            AppInsightsConfig = TelemetryConfiguration.Active;
            AppInsightsConfig.TelemetryInitializers.Add(new AppInsightsCloudIdInitializer());

            AppInsightsClient = new TelemetryClient(AppInsightsConfig);
            AppInsightsClient.Context.GlobalProperties.Add("node", Environment.MachineName);
            AppInsightsClient.Context.GlobalProperties.Add("provider", ServiceFabricEtw.ProviderName);

            EtwSession?.Dispose();
            EtwSession = new TraceEventSession(SessionName);

            //Note: TopShelf already calls Stop() when Cancel/unhandled exception occurs - so no need to do ETW cleanup as per: https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent.Samples/)
            //Use command line tool: "logman -ets" to monitor, and "logman -ets -stop ServiceFabric-Node-Monitor" to stop ETW sessions

            Task.Run(() =>
            {
                EtwSession.Source.Dynamic.All += ServiceFabricTraceEvent;

                EtwSession.EnableProviderForOperationalChannel();

                EtwSession.Source.Process();
            });
        }

        public void Stop()
        {
            EtwSession?.Dispose();
            EtwSession = null;
            AppInsightsClient?.Flush();
            AppInsightsConfig = null;
        }

        private void ServiceFabricTraceEvent(TraceEvent data)
        {
            var eventId = (ushort)data.ID;
            var eventIsRelevant =
            (
                (data.Level <= TraceEventLevel.Error && data.Level >= TraceEventLevel.Critical)
                || (
                    ServiceFabricEtw.Operational.ClusterEventsFilter(eventId, data) ||
                    ServiceFabricEtw.Operational.NodeEventsFilter(eventId, data)
                ));

            if (!eventIsRelevant)
            {
                return;
            }
            
            var properties = data.PayloadNames
                .Select((name, index) =>
                {
                    if (data.PayloadValue(index) is DateTime dateTimeValue)
                    {
                        return new KeyValuePair<string, string>(name, dateTimeValue.ToString("o"));
                    }
                    return new KeyValuePair<string, string>(name, data.PayloadString(index, Culture));
                })
                .ToDictionary(x => x.Key, x => x.Value);

            properties.Add("eventID", data.ID.ToString());
            properties.Add("name", data.EventName);
            properties.Add("timeStamp", data.TimeStamp.ToString("o"));

            AppInsightsClient.TrackTrace(data.FormattedMessage, data.ToSeverityLevel(), properties);
        }

        
    }
}
