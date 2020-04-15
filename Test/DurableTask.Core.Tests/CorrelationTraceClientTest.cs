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

namespace DurableTask.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using DurableTask.Core.Settings;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CorrelationTraceClientTest
    {
        bool distributedTracingState;
        [TestInitialize]
        public void Initialize()
        {
            this.distributedTracingState = CorrelationSettings.Current.EnableDistributedTracing;
            CorrelationSettings.Current.EnableDistributedTracing = true;
        }

        [TestCleanup]
        public void Cleanup()
        {
            CorrelationSettings.Current.EnableDistributedTracing = this.distributedTracingState;
        }

        [TestMethod]
        public void InterceptExternalTrackingNormalCase()
        {
            CorrelationSettings.Current.EnableDistributedTracing = true;
           var expectedActivity = new Activity("Some Activity");
            expectedActivity.Start();
            Activity receivedActivity = null;
            CorrelationTraceClient.SetUp(
                traceContextBase => { },
                traceContextBase => { },
                exception => { },
            activity => { receivedActivity = activity; });
            CorrelationTraceClient.InterceptExternalTracking(expectedActivity);
            Assert.AreEqual(expectedActivity.Id, receivedActivity?.Id, "The Activity.Id should be the same.");
        }

        [TestMethod]
        public void InterceptExternalTrackingShouldNotTrackedCase()
        {
            CorrelationSettings.Current.EnableDistributedTracing = false;
            var expectedActivity = new Activity("Some Activity");
            expectedActivity.Start();
            Activity receivedActivity = null;
            CorrelationTraceClient.SetUp(
                traceContextBase => { },
                traceContextBase => { },
                exception => { },
                activity => { receivedActivity = activity; });
            CorrelationTraceClient.InterceptExternalTracking(expectedActivity);
            Assert.IsNull(receivedActivity, "The Action should not be tracked.");
        }
    }
}
