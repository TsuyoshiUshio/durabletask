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
    using System;
    using System.Diagnostics;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.W3C;

    public static class ActivityExtensions
    {
        /// <summary>
        /// Create RequestTelemetry from the Activity.
        /// Currently W3C Trace context is supported. 
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public static RequestTelemetry CreateRequestTelemetry(this Activity activity)
        {
            var telemetry = new RequestTelemetry { Name = activity.OperationName };
            telemetry.Duration = activity.Duration;
            telemetry.Timestamp = activity.StartTimeUtc;
            // telemetry.StartTime = activity.StartTimeUtc;
            telemetry.Id = $"|{activity.GetTraceId()}.{activity.GetSpanId()}.";
            telemetry.Context.Operation.Id = activity.GetTraceId();
            telemetry.Context.Operation.ParentId = $"|{activity.GetTraceId()}.{activity.GetParentSpanId()}.";
            
            return telemetry;
        }

        /// <summary>
        /// Create DependencyTelemetry from the Activity.
        /// Currently W3C Trace context is supported.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public static DependencyTelemetry CreateDependencyTelemetry(this Activity activity)
        {
            var telemetry = new DependencyTelemetry { Name = activity.OperationName };
            telemetry.Start();
            // TODO Support Http correlation protocol. This logic is for W3CTraceContext
            telemetry.Duration = activity.Duration;
            telemetry.Timestamp = activity.StartTimeUtc; // TimeStamp is the time of ending the Activity.
            // telemetry.StartTime = activity.StartTimeUtc;
            telemetry.Id = $"|{activity.GetTraceId()}.{activity.GetSpanId()}.";
            telemetry.Context.Operation.Id = activity.GetTraceId();
            telemetry.Context.Operation.ParentId = $"|{activity.GetTraceId()}.{activity.GetParentSpanId()}.";

            return telemetry;
        }
    }
}
