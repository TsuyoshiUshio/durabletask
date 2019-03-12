//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace Correlation.Samples
{
    using System;
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
