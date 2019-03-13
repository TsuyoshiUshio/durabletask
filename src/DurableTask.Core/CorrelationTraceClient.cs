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

namespace DurableTask.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reactive;
    using DurableTask.AzureStorage;

    /// <summary>
    /// Delegate sending telemetry to the other side.
    /// Mainly send telemetry to the Durable Functions TelemetryClient
    /// </summary>
    public class CorrelationTraceClient
    {
        const string DiagnosticSourceName = "DurableTask.AzureStorage";
        const string RequestTrackEvent = "RequestEvent";
        const string RequestTrackEventWithCorrelationTrace = "RequestEventWithCorreationTrace";
        const string DependencyTrackEvent = "DependencyEvent";
        const string ExceptionEvent = "ExceptionEvent";
        static DiagnosticSource logger = new DiagnosticListener(DiagnosticSourceName);
        static IDisposable applicationInsightsSubscription = null;
        static IDisposable listenerSubscription = null;

        /// <summary>
        /// Setup this class uses callbacks to enable send telemetry to the Application Insights.
        /// You need to call this method if you want to use this class. 
        /// </summary>
        /// <param name="trackRequestTelemetryAction">Action to send request telemetry using <see cref="Activity"></see></param>
        /// <param name="trackRequestTelemetryActionWithCorrelationTrace">Action to rend request telemetry using <see cref="CorrelationTraceContext"></see></param>
        /// <param name="trackDependencyTelemetryAction">Action to send telemetry for <see cref="Activity"/></param>
        /// <param name="trackExceptionAction">Action to send telemetry for exception </param>
        public static void SetUp(
            Action<Activity> trackRequestTelemetryAction, 
            Action<CorrelationTrace> trackRequestTelemetryActionWithCorrelationTrace,
            Action<Activity> trackDependencyTelemetryAction, 
            Action<Exception> trackExceptionAction)
        {
            listenerSubscription = DiagnosticListener.AllListeners.Subscribe(
                delegate(DiagnosticListener listener)
                {
                    if (listener.Name == DiagnosticSourceName)
                    {
                        applicationInsightsSubscription?.Dispose();

                        applicationInsightsSubscription = listener.Subscribe((KeyValuePair<string, object> evt) =>
                        {
                            if (evt.Key == RequestTrackEvent)
                            {
                                var requestActivity = (Activity)evt.Value;
                                trackRequestTelemetryAction(requestActivity);
                            }

                            if (evt.Key == RequestTrackEventWithCorrelationTrace)
                            {
                                var requestCorrelationTrace = (CorrelationTrace)evt.Value;
                                trackRequestTelemetryActionWithCorrelationTrace(requestCorrelationTrace);
                            }

                            if (evt.Key == DependencyTrackEvent)
                            {
                                // the parameter is DependencyTelemetry which is already stopped. 
                                var dependencyActivity = (Activity) evt.Value;
                                trackDependencyTelemetryAction(dependencyActivity);
                            }

                            if (evt.Key == ExceptionEvent)
                            {
                                var e = (Exception) evt.Value;
                                trackExceptionAction(e);
                            }
                        });
                    }
                });
        }

        /// <summary>
        /// Track the RequestTelemetry
        /// </summary>
        /// <param name="activity"></param>
        public static void TrackRequestTelemetry(Activity activity)
        {
            logger.Write(RequestTrackEvent, activity);
        }

        /// <summary>
        /// Track the RequestTelemetry
        /// </summary>
        /// <param name="requestCorrelationTrace"></param>
        public static void TrackRequestTelemetry(CorrelationTrace requestCorrelationTrace)
        {
            logger.Write(RequestTrackEventWithCorrelationTrace, requestCorrelationTrace);
        }

        /// <summary>
        /// Track the DependencyTelemetry
        /// </summary>
        /// <param name="activity"></param>
        public static void TrackDepencencyTelemetry(Activity activity)
        {
            logger.Write(DependencyTrackEvent, activity);
        }

        /// <summary>
        /// Track the Exception
        /// </summary>
        /// <param name="e"></param>
        public static void TrackException(Exception e)
        {
            logger.Write(ExceptionEvent, e);
        }
    }
}
