using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace ServiceFabricMonitorService
{
    /// <summary>
    /// This initializer ensures that each SF Node is uniquely identifiable and tagged with unique cloud role instance/name
    /// If this is not done, all metrics for each cluster node in Azure ends up being under one generic namespace
    /// </summary>
    public class AppInsightsCloudIdInitializer : ITelemetryInitializer
    {
        private readonly string _cloudRoleName;
        private readonly string _cloudRoleInstance;

        public AppInsightsCloudIdInitializer()
        {
            _cloudRoleName = $"SF.Node.{Environment.MachineName}";
            _cloudRoleInstance = Environment.MachineName;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is MetricTelemetry metric)
            {
                metric.MetricNamespace = _cloudRoleName;
            }
            if (string.IsNullOrWhiteSpace(telemetry.Context.Cloud.RoleInstance) || string.IsNullOrWhiteSpace(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleInstance = _cloudRoleInstance;
                telemetry.Context.Cloud.RoleName = _cloudRoleName;
            }
        }
    }
}