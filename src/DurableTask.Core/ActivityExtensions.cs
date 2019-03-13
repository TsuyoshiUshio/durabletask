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

namespace DurableTask.Core
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.ApplicationInsights.W3C;

    /// <summary>
    /// Extension methods for Activity
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Set the <see cref="TraceContext"/>  as a Parent and Start Activity.
        /// This method support Both the HTTPCorrelation protocol and the W3C TraceContext.
        /// </summary>
        /// <param name="activity">For Extension methods</param>
        /// <param name="context">TraceContext instance.</param>
        /// <returns><see cref="Activity"/></returns>
        public static Activity SetParentAndStartActivity(this Activity activity, TraceContext context)
        {
            activity.SetParentId(context.ParentId);
            activity.SetTraceparent(context.Traceparent);
            activity.SetTracestate(context.Tracestate);
            activity.Start();
            return activity;
        }

        /// <summary>
        /// Set the <see cref="TraceContext"/>  as a Parent and Start Activity.
        /// This method support Both the HTTPCorrelation protocol and the W3C TraceContext.
        /// </summary>
        /// <param name="activity">For Extension methods</param>
        /// <param name="parent">TraceContext instance.</param>
        /// <returns><see cref="Activity"/></returns>
        public static Activity SetParentAndStartActivity(this Activity activity, Activity parent)
        {
            activity.SetParentId(parent.Id);
            activity.SetTraceparent(parent.GetTraceparent());
            activity.SetTracestate(parent.GetTracestate());
            activity.Start();
            return activity;
        }

        /// <summary>
        /// Create TraceContext from Activity and parent TraceContext.
        /// This method copy <see cref="TraceContext"/>.OrchestrationTraceContexts from TraceContext.
        /// </summary>
        /// <param name="activity">Activity which has already started.</param>
        /// <param name="parentTraceContext">Parent TraceContext</param>
        /// <returns></returns>
        public static TraceContext CreateTraceContext(this Activity activity, TraceContext parentTraceContext)
        {
            var context = new TraceContext()
            {
                StartTime = DateTimeOffset.UtcNow,
                Traceparent = activity.GetTraceparent(),
                Tracestate = activity.GetTracestate(),
                ParentSpanId = activity.GetParentSpanId(),
                ParentId = activity.Id,
                OrchestrationTraceContexts = parentTraceContext.OrchestrationTraceContexts.Clone<TraceContext>()
            };
            return context;
        }

        /// <summary>
        /// Create TraceContext from Activity and parent TraceContext.
        /// This method copy <see cref="TraceContext"/>.OrchestrationTraceContexts from TraceContext.
        /// </summary>
        /// <param name="activity">Activity which has already started.</param>
        /// <returns></returns>
        public static TraceContext CreateTraceContext(this Activity activity)
        {
            var context = new TraceContext()
            {
                StartTime = DateTimeOffset.UtcNow,
                Traceparent = activity.GetTraceparent(),
                Tracestate = activity.GetTracestate(),
                ParentSpanId = activity.GetParentSpanId(),
                ParentId = activity.Id
            };
            return context;
        }

        /// <summary>
        /// Set current instance to the Activity Current.
        /// This feature will be implemented on the Activity.
        /// </summary>
        /// <param name="activity"></param>
        public static void SetActivityCurrent(this Activity activity)
        {
            var property = typeof(Activity).GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            property.SetValue(null, activity);
        }
    }
}
