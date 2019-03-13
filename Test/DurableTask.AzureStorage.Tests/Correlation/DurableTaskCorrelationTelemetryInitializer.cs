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

#pragma warning disable 618

namespace DurableTask.AzureStorage.Tests.Correlation
{
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.W3C;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using DurableTask.Core;

    /// <summary>
    /// TelemetryInitializer with W3C TraceContext support.
    /// Customized code for <see cref="W3COperationCorrelationTelemetryInitializer"/>
    /// </summary>
    public class DurableTaskCorrelationTelemetryInitializer : ITelemetryInitializer
    {
        const string RddDiagnosticSourcePrefix = "rdddsc";
        const string SqlRemoteDependencyType = "SQL";

        /// <summary>Initializes telemety item.</summary>
        /// <param name="telemetry">Telemetry item.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (!(telemetry is RequestTelemetry))
            {
                Activity current = Activity.Current;
                if (current == null)
                {
                    if (CorrelationTraceContext.Current != null)
                    {
                        DurableTaskCorrelationTelemetryInitializer.UpdateTelemetry(telemetry, CorrelationTraceContext.Current);
                    }
                }
                else
                    DurableTaskCorrelationTelemetryInitializer.UpdateTelemetry(telemetry, current, false);
            }
        }

        internal static void UpdateTelemetry(ITelemetry telemetry, TraceContext context)
        {
            OperationTelemetry operationTelemetry1 = telemetry as OperationTelemetry;
            bool flag = operationTelemetry1 != null;
            if (flag)
                flag = ((flag ? 1 : 0) & (!(operationTelemetry1 is DependencyTelemetry dependencyTelemetry) || !(dependencyTelemetry.Type == "SQL") ? 1 : (!dependencyTelemetry.Context.GetInternalContext().SdkVersion.StartsWith("rdddsc", StringComparison.Ordinal) ? 1 : 0))) != 0;

            if (!string.IsNullOrEmpty(context.Tracestate))
            {
                operationTelemetry1.Properties["w3c_tracestate"] = context.Tracestate;
            }

            var traceparent = context.Traceparent.ToTraceparent();

            if (flag)
            {
                operationTelemetry1.Id = StringUtilities.FormatRequestId(telemetry.Context.Operation.Id, traceparent.SpanId);
                if (string.IsNullOrEmpty(context.ParentSpanId))
                {
                    telemetry.Context.Operation.ParentId = StringUtilities.FormatRequestId(telemetry.Context.Operation.Id, context.ParentSpanId);
                }
            }
            else
            {
                if (telemetry.Context.Operation.Id == null)
                {
                    telemetry.Context.Operation.Id = traceparent.TraceId;
                }

                telemetry.Context.Operation.ParentId = StringUtilities.FormatRequestId(telemetry.Context.Operation.Id, traceparent.SpanId);
            }
        }

        internal static void UpdateTelemetry(ITelemetry telemetry, Activity activity, bool forceUpdate)
        {
            if (activity == null)
                return;
            activity.UpdateContextOnActivity();
            OperationTelemetry operationTelemetry1 = telemetry as OperationTelemetry;
            bool flag = operationTelemetry1 != null;
            if (flag)
                flag = ((flag ? 1 : 0) & (!(operationTelemetry1 is DependencyTelemetry dependencyTelemetry) || !(dependencyTelemetry.Type == "SQL") ? 1 : (!dependencyTelemetry.Context.GetInternalContext().SdkVersion.StartsWith("rdddsc", StringComparison.Ordinal) ? 1 : 0))) != 0;
            string spanId1 = (string)null;
            string spanId2 = (string)null;
            
            foreach (KeyValuePair<string, string> tag in activity.Tags)
            {
                string key = tag.Key;
                if (!(key == "w3c_traceId"))
                {
                    if (!(key == "w3c_spanId"))
                    {
                        if (!(key == "w3c_parentSpanId"))
                        {
                            if (key == "w3c_tracestate" && telemetry is OperationTelemetry operationTelemetry2)
                                operationTelemetry2.Properties["w3c_tracestate"] = tag.Value;
                        }
                        else
                            spanId2 = tag.Value;
                    }
                    else
                        spanId1 = tag.Value;
                }
                else
                    telemetry.Context.Operation.Id = tag.Value;
            }
            if (flag)
            {
                operationTelemetry1.Id = StringUtilities.FormatRequestId(telemetry.Context.Operation.Id, spanId1);
                if (spanId2 != null)
                    telemetry.Context.Operation.ParentId = StringUtilities.FormatRequestId(telemetry.Context.Operation.Id, spanId2);
            }
            else
                telemetry.Context.Operation.ParentId = StringUtilities.FormatRequestId(telemetry.Context.Operation.Id, spanId1);
            if (operationTelemetry1 == null)
                return;
            if (operationTelemetry1.Context.Operation.Id != activity.RootId)
                operationTelemetry1.Properties["ai_legacyRootId"] = activity.RootId;
            if (!(operationTelemetry1.Id != activity.Id))
                return;
            operationTelemetry1.Properties["ai_legacyRequestId"] = activity.Id;
        }
    }
}
