// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Grpc.Core;
using Grpc.Net.Client;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Stress.TelemetryService;

/// <summary>
/// Sends OTLP telemetry from multiple simulated service instances with different
/// signal permutations and staggered stop times. Useful for manually testing the
/// Manage Data dialog timestamp column.
///
/// Instance layout:
///   - staggered-svc / logs-traces   → sends logs + traces, stops after 10s
///   - staggered-svc / metrics-only  → sends metrics only, stops after 30s
///   - staggered-svc / logs-only     → sends logs only, stops after 20s
///   - staggered-svc / all-active    → sends logs + traces + metrics, never stops
/// </summary>
public class StaggeredTelemetrySender(ILogger<StaggeredTelemetrySender> logger, IConfiguration config) : BackgroundService
{
    [Flags]
    private enum SignalKind
    {
        Logs = 1,
        Traces = 2,
        Metrics = 4
    }

    private sealed record InstanceConfig(string InstanceId, SignalKind Signals, TimeSpan? StopAfter);

    private static readonly InstanceConfig[] s_instances =
    [
        new("logs-traces", SignalKind.Logs | SignalKind.Traces, TimeSpan.FromSeconds(10)),
        new("metrics-only", SignalKind.Metrics, TimeSpan.FromSeconds(30)),
        new("logs-only", SignalKind.Logs, TimeSpan.FromSeconds(20)),
        new("all-active", SignalKind.Logs | SignalKind.Traces | SignalKind.Metrics, null)
    ];

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var address = config["OTEL_EXPORTER_OTLP_ENDPOINT"]!;
        var channel = GrpcChannel.ForAddress(address);
        var logClient = new LogsService.LogsServiceClient(channel);
        var traceClient = new TraceService.TraceServiceClient(channel);
        var metricsClient = new MetricsService.MetricsServiceClient(channel);
        var otlpApiKey = config["OTEL_EXPORTER_OTLP_HEADERS"]!.Split('=')[1];
        var metadata = new Metadata { { "x-otlp-api-key", otlpApiKey } };

        var tasks = s_instances.Select(inst => RunInstanceAsync(inst, logClient, traceClient, metricsClient, metadata, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task RunInstanceAsync(
        InstanceConfig inst,
        LogsService.LogsServiceClient logClient,
        TraceService.TraceServiceClient traceClient,
        MetricsService.MetricsServiceClient metricsClient,
        Metadata metadata,
        CancellationToken cancellationToken)
    {
        using var linkedCts = inst.StopAfter is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (linkedCts is not null && inst.StopAfter is { } delay)
        {
            linkedCts.CancelAfter(delay);
        }

        var token = linkedCts?.Token ?? cancellationToken;
        var resource = CreateResource("staggered-svc", inst.InstanceId);
        var iteration = 0;

        logger.LogInformation("Staggered instance {InstanceId} starting — signals: {Signals}, stops after: {StopAfter}",
            inst.InstanceId, inst.Signals, inst.StopAfter?.ToString() ?? "never");

        try
        {
            while (!token.IsCancellationRequested)
            {
                iteration++;

                if (inst.Signals.HasFlag(SignalKind.Logs))
                {
                    await SendLogAsync(logClient, resource, inst.InstanceId, iteration, metadata, token);
                }

                if (inst.Signals.HasFlag(SignalKind.Traces))
                {
                    await SendTraceAsync(traceClient, resource, inst.InstanceId, metadata, token);
                }

                if (inst.Signals.HasFlag(SignalKind.Metrics))
                {
                    await SendMetricAsync(metricsClient, resource, inst.InstanceId, iteration, metadata, token);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        catch (OperationCanceledException) when (inst.StopAfter is not null)
        {
            logger.LogInformation("Staggered instance {InstanceId} stopped after {StopAfter}", inst.InstanceId, inst.StopAfter);
        }
    }

    private static async Task SendLogAsync(
        LogsService.LogsServiceClient client, Resource resource,
        string instanceId, int iteration, Metadata metadata, CancellationToken token)
    {
        var request = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    Resource = resource,
                    ScopeLogs =
                    {
                        new ScopeLogs
                        {
                            Scope = new InstrumentationScope { Name = "StaggeredLogger" },
                            LogRecords =
                            {
                                new LogRecord
                                {
                                    TimeUnixNano = TelemetryStresser.DateTimeToUnixNanoseconds(DateTime.UtcNow),
                                    SeverityNumber = SeverityNumber.Info,
                                    Body = new AnyValue { StringValue = $"[{instanceId}] heartbeat #{iteration}" }
                                }
                            }
                        }
                    }
                }
            }
        };

        await client.ExportAsync(request, headers: metadata, cancellationToken: token);
    }

    private static async Task SendTraceAsync(
        TraceService.TraceServiceClient client, Resource resource,
        string instanceId, Metadata metadata, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = resource,
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Scope = new InstrumentationScope { Name = "StaggeredTracer" },
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = GenerateId(16),
                                    SpanId = GenerateId(8),
                                    Name = $"heartbeat-{instanceId}",
                                    Kind = Span.Types.SpanKind.Internal,
                                    StartTimeUnixNano = TelemetryStresser.DateTimeToUnixNanoseconds(now),
                                    EndTimeUnixNano = TelemetryStresser.DateTimeToUnixNanoseconds(now.AddMilliseconds(5)),
                                    Status = new OpenTelemetry.Proto.Trace.V1.Status { Code = OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode.Ok }
                                }
                            }
                        }
                    }
                }
            }
        };

        await client.ExportAsync(request, headers: metadata, cancellationToken: token);
    }

    private static async Task SendMetricAsync(
        MetricsService.MetricsServiceClient client, Resource resource,
        string instanceId, int value, Metadata metadata, CancellationToken token)
    {
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = resource,
                    ScopeMetrics =
                    {
                        new ScopeMetrics
                        {
                            Scope = new InstrumentationScope { Name = "StaggeredMeter" },
                            Metrics =
                            {
                                TelemetryStresser.CreateSumMetric($"staggered-counter-{instanceId}", DateTime.UtcNow, value: value)
                            }
                        }
                    }
                }
            }
        };

        await client.ExportAsync(request, headers: metadata, cancellationToken: token);
    }

    private static Resource CreateResource(string name, string instanceId)
    {
        return new Resource
        {
            Attributes =
            {
                new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = name } },
                new KeyValue { Key = "service.instance.id", Value = new AnyValue { StringValue = instanceId } }
            }
        };
    }

    private static Google.Protobuf.ByteString GenerateId(int byteCount)
    {
        var bytes = new byte[byteCount];
        Random.Shared.NextBytes(bytes);
        return Google.Protobuf.ByteString.CopyFrom(bytes);
    }
}
