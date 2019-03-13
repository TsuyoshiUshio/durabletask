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
    using System.Diagnostics;
    using DurableTask.AzureStorage;
    using DurableTask.Core;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.W3C;

    public class TelemetryActivator
    {        
        private static TelemetryClient telemetryClient;

        public void Initialize()
        {
            SetUpTelemetryClient();
            SetUpTelemetryCallbacks();
        }

        void SetUpTelemetryCallbacks()
        {
            CorrelationTraceClient.SetUp(
                (Activity requestActivity) =>
                {
                    requestActivity.Stop();
                    var requestTelemetry = requestActivity.CreateRequestTelemetry();
                    telemetryClient.TrackRequest(requestTelemetry);
                },
                (CorrelationTrace requestCorrelationTrace) =>
                {
                    var requestTelemetry = requestCorrelationTrace.CreateRequestTelemetry();
                    telemetryClient.TrackRequest(requestTelemetry);
                },
                (Activity dependencyActivity) =>
                {
                    dependencyActivity.Stop();
                    var dependencyTelemetry = dependencyActivity.CreateDependencyTelemetry();
                    telemetryClient.TrackDependency(dependencyTelemetry);
                },
                (Exception e) =>
                {
                    telemetryClient.TrackException(e);
                }
            );
        }

        void SetUpTelemetryClient()
        {
            //            var module = new DependencyTrackingTelemetryModule();
            //            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            //            TelemetryConfiguration configAutoTracking = TelemetryConfiguration.CreateDefault();
            //            configAutoTracking.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            //#pragma warning disable 618
            //            configAutoTracking.TelemetryInitializers.Add(new W3COperationCorrelationTelemetryInitializer());
            //            module.Initialize(configAutoTracking);

            // module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("127.0.0.1");

            var module = new DependencyTrackingTelemetryModule();
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");

            TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();
            config.TelemetryInitializers.Add(new DurableTaskCorrelationTelemetryInitializer());
            config.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

            module.Initialize(config);

            telemetryClient = new TelemetryClient(config);
        }
    }
}
