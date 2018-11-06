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

namespace DurableTask.AzureStorage
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.ApplicationInsights.W3C;

    /// <summary>
    /// Add correlation features for MessageData
    /// </summary>
    public static class MessageDataExtensions
    {
        public static void SetupCausality(this MessageData message)
        {
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                var traceId = currentActivity.Tags.FirstOrDefault(t => t.Key == DurableCausalityManager.W3CTraceId).Value;
                var spanId = currentActivity.Tags.FirstOrDefault(t => t.Key == DurableCausalityManager.W3CSpanId).Value;
                if (string.IsNullOrEmpty(traceId) || string.IsNullOrEmpty(spanId))
                {
                    traceId = Guid.NewGuid().ToString("n");
                    spanId = Guid.NewGuid().ToString("n");
                }

                var tracestate = currentActivity.Tags.FirstOrDefault(t => t.Key == DurableCausalityManager.W3CTraceState).Value;
                message.SetTraceContext($"00-{traceId}-{spanId}-01", tracestate);
            }
        }

        public static void UpdateActivity(this MessageData message)
        {
            if (!string.IsNullOrEmpty(message.TraceContext?.Traceparent))
            {
                Activity.Current?.SetTraceparent(message.TraceContext.Traceparent);
                if (!string.IsNullOrEmpty(message.TraceContext?.Tracestate))
                {
                    Activity.Current?.SetTracestate(message.TraceContext.Tracestate);
                }
            }
            else
            {
                Activity.Current?.GenerateW3CContext();
            }
        }

        public static void SetOwner(this MessageData message, Guid functionOwner)
        {
            var traceContext = message.TraceContext ?? new TraceContext();
            traceContext.ParentId = functionOwner.ToString();
            message.TraceContext = traceContext;
        }

        public static void SetTraceContext(this MessageData message, string traceparent, string tracestate)
        {
            var traceContext = message.TraceContext ?? new TraceContext();
            traceContext.Traceparent = traceparent;
            traceContext.Tracestate = tracestate;
            message.TraceContext = traceContext;
        }
    }
}
