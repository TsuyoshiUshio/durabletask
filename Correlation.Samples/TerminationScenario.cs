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
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using Microsoft.ApplicationInsights.W3C;

    public class TerminationScenario
    {
        public async Task ExecuteAsync()
        {
            new TelemetryActivator().Initialize();

            using (
                TestOrchestrationHost host = TestHelpers.GetTestOrchestrationHost(false))
            {
                await host.StartAsync();
                var activity = new Activity("Start Orchestration");
#pragma warning disable 618
                activity.GenerateW3CContext();
#pragma warning restore 618
                activity.Start();
                var client = await host.StartOrchestrationAsync(typeof(TerminatedOrchestration), "50"); // TODO The parameter null will throw exception. (for the experiment)
                await Task.Delay(TimeSpan.FromSeconds(10));
                await client.TerminateAsync("I'd like to stop it.");
                var status = await client.WaitForCompletionAsync(TimeSpan.FromMinutes(10));

                await host.StopAsync();
            }
        }
    }

    [KnownType(typeof(WaitActivity))]
    internal class TerminatedOrchestration : TaskOrchestration<string, string>
    {
        public override async Task<string> RunTask(OrchestrationContext context, string input)
        {
            return await context.ScheduleTask<string>(typeof(WaitActivity), "");
        }
    }

    internal class WaitActivity : TaskActivity<string, string>
    {
        protected override string Execute(TaskContext context, string input)
        {
            return input;
        }

        protected override async Task<string> ExecuteAsync(TaskContext context, string input) {
            // Wait for 5 min for terminate. 
            await Task.Delay(TimeSpan.FromMinutes(1));

            Console.WriteLine($"Activity: Hello {input}");
            return $"Hello, {input}!";
        }
    }
}
