using System;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ServiceFabricMonitorService
{
    /// <summary>
    /// Service Fabric ETW-related declarations
    /// </summary>
    public static class ServiceFabricEtw
    {
        public const string ProviderName = "Microsoft-ServiceFabric";
        public static readonly Guid ProviderGuid = new Guid("CBD93BC2-71E5-4566-B3A7-595D8EECA6E8");

        /// <summary>
        /// Microsoft-ServiceFabric/Operational Channel
        /// </summary>
        public static class Operational
        {
            public const ulong Keyword = 0x4000_0000_0000_0001;

            public static Func<ushort, TraceEvent, bool> NodeEventsFilter => (eventId, data) =>
                (eventId >= 18602 && eventId <= 18607) || (eventId >= 25621 && eventId <= 25626);

            public static Func<ushort, TraceEvent, bool> ClusterEventsFilter => (eventId, data) =>
                (eventId >= 29627 && eventId <= 29631);

        }

        public static bool EnableProviderForOperationalChannel(this TraceEventSession session, TraceEventProviderOptions options = null)
        {
            return session.EnableProvider(ProviderName, TraceEventLevel.Informational, Operational.Keyword, options);
        }

        public static SeverityLevel ToSeverityLevel(this TraceEvent data)
        {
            switch (data.Level)
            {
                case TraceEventLevel.Always:
                case TraceEventLevel.Critical:
                    return SeverityLevel.Critical;
                case TraceEventLevel.Error:
                    return SeverityLevel.Error;
                case TraceEventLevel.Warning:
                    return SeverityLevel.Warning;
                case TraceEventLevel.Informational:
                    return SeverityLevel.Information;
                case TraceEventLevel.Verbose:
                    return SeverityLevel.Verbose;
                default:
                    throw new ArgumentOutOfRangeException($"{data.Level} mapping to {nameof(SeverityLevel)}");
            }
        }
    }
}