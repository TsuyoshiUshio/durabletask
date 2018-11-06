using System;
using System.Collections.Generic;
using System.Text;

namespace DurableTask.AzureStorage.Tests.Correlation
{
    using System.Diagnostics;
    using DurableTask.AzureStorage;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.W3C;

    public static class CorrelationTraceExtensions
    {
        /// <summary>
        /// Create RequestTelemetry from the Activity.
        /// Currently W3C Trace context is supported. 
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public static RequestTelemetry CreateRequestTelemetry(this CorrelationTrace correlationTrace)
        {
            var telemetry = new RequestTelemetry { Name = correlationTrace.OperationName};
            
            telemetry.Timestamp = correlationTrace.TraceContext.StartTime;
            telemetry.Duration = DateTimeOffset.UtcNow - telemetry.Timestamp;
            Traceparent traceParent = correlationTrace.TraceContext.Traceparent.ToTraceparent();
            telemetry.Id = $"|{traceParent.TraceId}.{traceParent.SpanId}.";

            telemetry.Context.Operation.Id = traceParent.TraceId;
            telemetry.Context.Operation.ParentId = $"|{traceParent.TraceId}.{correlationTrace.TraceContext.ParentSpanId}.";

            return telemetry;
        }
    }
}
