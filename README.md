# Service Fabric App Insights Health Monitor

Windows Service to send Service Fabric Cluster and Node health information to Azure App Insights. 

The purpose of this project is to simplify monitoring of standalone Service Fabric clusters running on MS Windows. Although aimed at on-premise clusters, it will also work for clusters hosted in Azure VMs.

# Background

When running a micro-service orchestrator, like Service Fabric, it's critical that you keep tabs on the health of all your cluster nodes.
If you're fortunate enough to run your cluster in Azure, then life is [much simpler for you](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-diagnostics-oms-setup)

On the other hand, if you're running your cluster on-premise on Windows, getting  node health info reported to Application Insights is not as simple as it should be. [Event Store](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-diagnostics-eventstore) is not available for stand-alone clusters. 

[Event Flow](https://github.com/Azure/diagnostics-eventflow) seems to be the recommended approach  and, on the surface, seems like a good solution... Unfortunatly, I found that: 
- filtering events was hard/limited, 
- regex expressions didn't appear to work.
- suffers from common denominator problem - i.e. because it supports multiple inputs/outputs, it provides generic ways for filtering events, although this is not always powerful enough to best utilize all features of a particular provider.
- Not as light weight as using ETW directly

# Overview

This project provides a simple windows service that can be used to forward Service Fabric cluster health and performance counter information to Application Insights (AI).

Simply install and run this service on each of your windows Service fabric cluster nodes.

At this stage, this monitoring service is fairly basic and only provides standard AI configuration options. That said, if you want to invest more time in it, pull requests are welcome!

> **Note:** This project is ONLY concerned with monitoring of fabric cluster nodes. For applications and services running on service fabric, I highly recommend [ApplicationInsights-ServiceFabric](https://github.com/microsoft/ApplicationInsights-ServiceFabric).

> **Aside:** I also toyed with the idea of using Service Fabric itself to host the monitoring service. The benefit would be that it's easy to deploy on all your nodes! Unfortunately, the problem with this approach is that you'll likely miss some of the cluster node up/down type events. As such, I opted for the windows service approach to ensure the lifecycle of monitoring is decoupled from the Service Fabric host and cluster health.

# Features
- Uses the standard [Microsoft.ApplicationInsights](https://www.nuget.org/packages/Microsoft.ApplicationInsights/) and [Microsoft.ApplicationInsights.PerfCounterCollector](https://www.nuget.org/packages/Microsoft.ApplicationInsights.PerfCounterCollector/) nuget packages to publish information to AI. This means you can use the usual configuration options to modify performance counters, instrumentation key, etc 
ETW and important performance counter output to Application Insights
- Only sends the most important events from the Service-Fabric Operational ETW channel:
  - [Node events](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-diagnostics-event-generation-operational#node-events) 
  - [Cluster events](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-diagnostics-event-generation-operational#cluster-events)
- Some of the performance counters listed in [MSDocs Service Fabric Performance Metrics](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-diagnostics-event-generation-perf). To add additional ones, simply update the _ApplicationInsights.config_ file.

# Setup Instructions 

This project uses the excellent [TopShelf](https://topshelf.readthedocs.io) framework to simplify the development of windows services. As such, the monitoring executable supports all the standard Topshelf [command line](https://topshelf.readthedocs.io/en/latest/overview/commandline.html) options.

## Install
ServiceFabricMonitorService.exe install -username "DOMAIN\YourServiceAccount" -password "Some Secret"

## Starting the Service
ServiceFabricMonitorService.exe start

## Uninstall
ServiceFabricMonitorService.exe uninstall

# App Insights
Some KQL (Kusto) queries to help you get started with creating dashboards in App Insights

## Daily SF Node StateTransitions

Below query helps identifying troubled nodes by identifying nodes that when up/down. Likely you want the dashboard to show data from last X days

```
traces
| extend node = tostring(customDimensions.['nodeName'])
| extend eventName = tostring(customDimensions.['eventName'])
| extend unit = format_datetime(timestamp, 'MM/dd')
| where severityLevel <= 3 and 
| where message contains "StateTransition" 
| project node, eventName, unit
| summarize state_changes=count() by node, eventName, unit
| project node, eventName, state_changes, unit
| sort by node asc
```

Alternatively, if you want to do counts by actual type of node stransitions, the below is a good starting point. Note, there are quite a few different transitions beyond simply Up/down that indicate success or failure, so you may wish to accumulate transitions by groups of event names instead. 

```
traces
| extend node = tostring(customDimensions.['node'])
| extend eventName = tostring(customDimensions.['eventName'])
| extend unit = format_datetime(timestamp, 'MM/dd')
| where severityLevel <= 3
| where message contains "StateTransition" 
| summarize openSucc=countif(eventName == "NodeOpenSucceeded"), openFail=countif(eventName == "NodeOpenFailed"), 
aborted=countif(eventName == "NodeAborted"), up=countif(eventName == "NodeUp"), down=countif(eventName == "NodeDown") 
by node, unit
```

## Average CPU per SF Node

The below query returns average CPU Total Processor performance counter metrics, averaged over 10 minute time intervals

```
metrics
| where name == "Total Processor Time"
| where cloud_RoleName startswith "SF.Node."
| summarize Avg_CPU=avg(value*100) 
by cloud_RoleInstance, bin(timestamp, 10m)
| sort by cloud_RoleInstance asc
```

## Available Memory per SF Node

Returns the available memory (in GB) per node, averaged over 10 minute intervals

```
metrics
| where cloud_RoleName startswith "SF.Node."
| where name == "Memory - Available MBytes"
| summarize Min_Free_Gb=min(value/1000.0)
by cloud_RoleInstance, bin(timestamp, 10m)
| sort by cloud_RoleInstance asc
```

## Free Logical Disk space per Node

Per node minimum percentage of free disk space

```
metrics
| where cloud_RoleName startswith "SF.Node."
| where name == "LogicalDisk(_Total) - % Free Space"
| summarize Percent_Free=min(value)
by cloud_RoleInstance, bin(timestamp, 30m)
| sort by cloud_RoleInstance asc
```

##  Average Disk Read/Write Queue length

Per node average disk read queue length. Helps identifying I/O slowness related to reading data from disk

```
metrics
| where cloud_RoleName startswith "SF.Node."
| where name == "PhysicalDisk(_Total) - Avg. Disk Read Queue Length"
| summarize Read_Queue_Length=max(value)
by cloud_RoleInstance, bin(timestamp, 5m)
| sort by cloud_RoleInstance asc
```

Similarly, using `name == "PhysicalDisk(_Total) - Avg. Disk Write Queue Length"` will give you write queue stats.
