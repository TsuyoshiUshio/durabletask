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

namespace DurableTask.AzureStorage.Tests.Correlation
{
    using System;
    using System.CodeDom;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.W3C;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable 618

    [TestClass]
    public class CorrelationScenarioTest
    {
        [TestMethod]
        public async Task SingleOrchestratorWithSingleActivityAsync()
        {
            var actual = await ExecuteOrchestrationAsync(typeof(SayHelloActivity), "world", 50);
            Assert.AreEqual(6, actual.Count);
            Assert.AreEqual(TraceMessages.Client, actual[0].Name); // Start Orchestration
            Assert.AreEqual(TraceMessages.Client, actual[1].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} SayHelloActivity", actual[2].Name); // Orchestrator start
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[3].Name); // Activity Start
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[4].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} SayHelloActivity", actual[5].Name); // Orchestrator Stop
        }

        [KnownType(typeof(Hello))]
        internal class SayHelloActivity : TaskOrchestration<string, string>
        {
            public override Task<string> RunTask(OrchestrationContext context, string input)
            {
                return context.ScheduleTask<string>(typeof(Hello), input);
            }
        }

        internal class Hello : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    throw new ArgumentNullException(nameof(input));
                }

                Console.WriteLine($"Activity: Hello {input}");
                return $"Hello, {input}!";
            }
        }

        [TestMethod]
        public async Task SingleOrchestrationWithThrowingExceptionAsync()
        {
            // parameter = null cause an exception. 
            var result = await ExecuteOrchestrationWithExceptionAsync(typeof(SayHelloActivity), null, 50);

            var actual = result.Item1;
            var actualExceptions = result.Item2;

            Assert.AreEqual(6, actual.Count);
            var index = 0;
            Assert.AreEqual(TraceMessages.Client, actual[index].Name); // Start Orchestration Request
            Assert.AreEqual(TraceMessages.Client, actual[++index].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} SayHelloActivity", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[++index].Name); // Activity Dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} SayHelloActivity", actual[++index].Name); // Orchestrator Dependency            

            Assert.AreEqual(actualExceptions[0].Context.Operation.ParentId, actual[3].Id);
            Assert.AreEqual(actualExceptions[1].Context.Operation.ParentId, actual[2].Id);
        }

        [TestMethod]
        public async Task SingleOrchestratorWithMultipleActivitiesAsync()
        {
            var actual = await ExecuteOrchestrationAsync(typeof(SayHelloActivities), "world", 50);
            Assert.AreEqual(8, actual.Count);

            // Client Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[0].GetType().ToString());
            Assert.AreEqual(TraceMessages.Client, actual[0].Name);
            // Client Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[1].GetType().ToString());
            Assert.AreEqual(TraceMessages.Client, actual[1].Name);
            // Orchestrator Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[2].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} SayHelloActivities", actual[2].Name);
            // Activity 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[3].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} HelloWait", actual[3].Name);
            // Activity 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[4].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} HelloWait", actual[4].Name);
            // Activity 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[5].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} HelloWait", actual[5].Name);
            // Activity 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[6].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} HelloWait", actual[6].Name);
            // Orchestrator Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[7].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} SayHelloActivities", actual[7].Name);
        }

        [KnownType(typeof(HelloWait))]
        internal class SayHelloActivities : TaskOrchestration<string, string>
        {
            public override async Task<string> RunTask(OrchestrationContext context, string input)
            {
                var tasks = new List<Task<string>>();
                tasks.Add(context.ScheduleTask<string>(typeof(HelloWait), input));
                tasks.Add(context.ScheduleTask<string>(typeof(HelloWait), input));
                await Task.WhenAll(tasks);
                return $"{tasks[0].Result}:{tasks[1].Result}";
            }
        }

        internal class HelloWait : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input)
            {
                throw new NotImplementedException();
            }

            protected override async Task<string> ExecuteAsync(TaskContext context, string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    throw new ArgumentNullException(nameof(input));
                }

                await Task.Delay(TimeSpan.FromSeconds(2));

                Console.WriteLine($"Activity: HelloWait {input}");
                return $"Hello, {input}! I wait for 1 sec.";
            }
        }

        [TestMethod]
        public async Task SubOrchestratorAsync()
        {
            var actual = await ExecuteOrchestrationAsync(typeof(ParentOrchestrator), "world", 50);
            Assert.AreEqual(8, actual.Count);
            // Client Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[0].GetType().ToString());
            Assert.AreEqual(TraceMessages.Client, actual[0].Name);
            // Client Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[1].GetType().ToString());
            Assert.AreEqual(TraceMessages.Client, actual[1].Name);
            // Orchestrator Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[2].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ParentOrchestrator", actual[2].Name);
            // Activity 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[3].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestrator", actual[3].Name);
            // Activity 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[4].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[4].Name);
            // Activity 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[5].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[5].Name);
            // Activity 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[6].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestrator", actual[6].Name);
            // Orchestrator Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[7].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ParentOrchestrator", actual[7].Name);
        }

        [KnownType(typeof(ChildOrchestrator))]
        [KnownType(typeof(Hello))]
        internal class ParentOrchestrator : TaskOrchestration<string, string>
        {
            public override Task<string> RunTask(OrchestrationContext context, string input)
            {
                return context.CreateSubOrchestrationInstance<string>(typeof(ChildOrchestrator), input);
            }
        }

        [KnownType(typeof(Hello))]
        internal class ChildOrchestrator : TaskOrchestration<string, string>
        {
            public override Task<string> RunTask(OrchestrationContext context, string input)
            {
                return context.ScheduleTask<string>(typeof(Hello), input);
            }
        }

        [TestMethod]
        public async Task MultipleSubOrchestratorAsync()
        {
            var actual = await ExecuteOrchestrationAsync(typeof(ParentOrchestratorWithMultiLayeredSubOrchestrator), "world", 50);
            Assert.AreEqual(14, actual.Count);
            // Client Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[0].GetType().ToString());
            Assert.AreEqual(TraceMessages.Client, actual[0].Name);
            // Client Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[1].GetType().ToString());
            Assert.AreEqual(TraceMessages.Client, actual[1].Name);
            // Parent Orchestrator Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[2].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ParentOrchestratorWithMultiLayeredSubOrchestrator", actual[2].Name);
            // Child Orchestrator Level 1 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[3].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestratorWithMultiSubOrchestrator", actual[3].Name);
            // Child Orchestrator Level 2 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[4].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestrator", actual[4].Name);
            // Activity 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[5].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[5].Name);
            // Activity 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[6].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[6].Name);
            // Child Orchestrator Level 2 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[7].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestrator", actual[7].Name);
           // Child Orchestrator Level 2 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[8].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestrator", actual[8].Name);
            // Activity 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[9].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[9].Name);
            // Activity 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[10].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Activity} Hello", actual[10].Name);
            // Child Orchestrator Level 2 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[11].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestrator", actual[11].Name);
            // Child Orchestrator Level 1 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[12].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ChildOrchestratorWithMultiSubOrchestrator", actual[12].Name);
            // Orchestrator Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[13].GetType().ToString());
            Assert.AreEqual($"{TraceMessages.Orchestrator} ParentOrchestratorWithMultiLayeredSubOrchestrator", actual[13].Name);
        }

        [KnownType(typeof(ChildOrchestratorWithMultiSubOrchestrator))]
        [KnownType(typeof(ChildOrchestrator))]
        [KnownType(typeof(Hello))]
        internal class ParentOrchestratorWithMultiLayeredSubOrchestrator : TaskOrchestration<string, string>
        {
            public override Task<string> RunTask(OrchestrationContext context, string input)
            {
                return context.CreateSubOrchestrationInstance<string>(typeof(ChildOrchestratorWithMultiSubOrchestrator), input);
            }
        }

        [KnownType(typeof(ChildOrchestrator))]
        [KnownType(typeof(Hello))]
        internal class ChildOrchestratorWithMultiSubOrchestrator : TaskOrchestration<string, string>
        {
            public override async Task<string> RunTask(OrchestrationContext context, string input)
            {
                var tasks = new List<Task<string>>();
                tasks.Add(context.CreateSubOrchestrationInstance<string>(typeof(ChildOrchestrator), "foo"));
                tasks.Add(context.CreateSubOrchestrationInstance<string>(typeof(ChildOrchestrator), "bar"));
                await Task.WhenAll(tasks);
                return $"{tasks[0].Result}:{tasks[1].Result}";
            }
        }

        [TestMethod]
        public async Task SingleOrchestratorWithRetryAsync()
        {
            var resultTuple = await ExecuteOrchestrationWithExceptionAsync(typeof(SingleOrchestrationWithRetry), "world", 50);
            var actual = resultTuple.Item1;
            var actualExceptions = resultTuple.Item2;
            Assert.AreEqual(8, actual.Count);
            Assert.AreEqual(TraceMessages.Client, actual[0].Name); // Start Orchestration
            Assert.AreEqual(TraceMessages.Client, actual[1].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} SingleOrchestrationWithRetry", actual[2].Name); // Orchestrator start
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice", actual[3].Name); // Activity Start
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice", actual[4].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice", actual[5].Name); // Activity Start
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice", actual[6].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} SingleOrchestrationWithRetry", actual[7].Name); // Orchestrator Stop

            Assert.AreEqual(actualExceptions[0].Context.Operation.ParentId, actual[3].Id);
            Assert.AreEqual(actualExceptions[1].Context.Operation.ParentId, actual[2].Id);
        }

        [KnownType(typeof(NeedToExecuteTwice))]
        internal class SingleOrchestrationWithRetry : TaskOrchestration<string, string>
        {
            public override Task<string> RunTask(OrchestrationContext context, string input)
            {
                var retryOption = new RetryOptions(TimeSpan.FromMilliseconds(10), 2);        
                return context.ScheduleWithRetry<string>(typeof(NeedToExecuteTwice), retryOption, input);
            }
        }

        internal class NeedToExecuteTwice : TaskActivity<string, string>
        {
            static int Counter = 0;

            protected override string Execute(TaskContext context, string input)
            {
                if (Counter == 0)
                {
                    Counter++;
                    throw new Exception("Something happens");
                }

                return $"Hello {input} with retry";
            }
        }

        [TestMethod]
        public async Task MultiLayeredOrchestrationWithRetryAsync()
        {
            var resultTuple = await ExecuteOrchestrationWithExceptionAsync(typeof(MultiLayeredOrchestrationWithRetry), "world", 50);
            var actual = resultTuple.Item1;
            var actualExceptions = resultTuple.Item2;
            Assert.AreEqual(20, actual.Count);
            var index = 0;
            Assert.AreEqual(TraceMessages.Client, actual[index].Name); // Start Orchestration Request
            Assert.AreEqual(TraceMessages.Client, actual[++index].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationWithRetry", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Dependency 
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Request Retry
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish Request
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Dependency Retry
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Request Retry
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish Request
            Assert.AreEqual($"{TraceMessages.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator dependency
            Assert.AreEqual($"{TraceMessages.Orchestrator} MultiLayeredOrchestrationWithRetry", actual[++index].Name); // Orchestrator dependency

            Assert.AreEqual(actualExceptions[0].Context.Operation.ParentId,actual[4].Id);
            Assert.AreEqual(actualExceptions[1].Context.Operation.ParentId, actual[3].Id);
            Assert.AreEqual(actualExceptions[2].Context.Operation.ParentId, actual[10].Id);
            Assert.AreEqual(actualExceptions[3].Context.Operation.ParentId, actual[7].Id);
        }

        [KnownType(typeof(MultiLayeredOrchestrationChildWithRetry))]
        [KnownType(typeof(NeedToExecuteTwice01))]
        [KnownType(typeof(NeedToExecuteTwice02))]
        internal class MultiLayeredOrchestrationWithRetry : TaskOrchestration<string, string>
        {
            public override Task<string> RunTask(OrchestrationContext context, string input)
            {
                var retryOption = new RetryOptions(TimeSpan.FromMilliseconds(10), 3);
                return context.CreateSubOrchestrationInstanceWithRetry<string>(typeof(MultiLayeredOrchestrationChildWithRetry), retryOption, input);
            }
        }

        [KnownType(typeof(NeedToExecuteTwice01))]
        [KnownType(typeof(NeedToExecuteTwice02))]
        internal class MultiLayeredOrchestrationChildWithRetry : TaskOrchestration<string, string>
        {
            public override async Task<string> RunTask(OrchestrationContext context, string input)
            {
                var result01 = await context.ScheduleTask<string>(typeof(NeedToExecuteTwice01), input);
                var result02 = await context.ScheduleTask<string>(typeof(NeedToExecuteTwice02), input);
                return $"{result01}:{result02}";
            }
        }

        internal class NeedToExecuteTwice01 : TaskActivity<string, string>
        {
            static int Counter = 0;

            protected override string Execute(TaskContext context, string input)
            {
                if (Counter == 0)
                {
                    Counter++;
                    throw new Exception("Something happens");
                }

                return $"Hello {input} with retry";
            }
        }

        internal class NeedToExecuteTwice02 : TaskActivity<string, string>
        {
            static int Counter = 0;

            protected override string Execute(TaskContext context, string input)
            {
                if (Counter == 0)
                {
                    Counter++;
                    throw new Exception("Something happens");
                }

                return $"Hello {input} with retry";
            }
        }

        //[TestMethod] ContinueAsNew
        //[TestMethod] terminate

        async Task<Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>>> ExecuteOrchestrationWithExceptionAsync(Type orchestrationType, string parameter, int timeout)
        {
            var sendItems = new ConcurrentQueue<ITelemetry>();
            await ExtractTelemetry(orchestrationType, parameter, timeout, sendItems);

            var sendItemList = ConvertTo(sendItems);
            var operationTelemetryList = sendItemList.OfType<OperationTelemetry>();
            var exceptionTelemetryList = sendItemList.OfType<ExceptionTelemetry>().ToList();

            List<OperationTelemetry> operationTelemetries = FilterOperationTelemetry(operationTelemetryList).ToList().CorrelationSort();

            return new Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>>(operationTelemetries, exceptionTelemetryList);
        }



        async Task<List<OperationTelemetry>> ExecuteOrchestrationAsync(Type orchestrationType, string parameter, int timeout)
        {
            var sendItems = new ConcurrentQueue<ITelemetry>();
            await ExtractTelemetry(orchestrationType, parameter, timeout, sendItems);

            var sendItemList = ConvertTo(sendItems);
            var operationTelemetryList = sendItemList.OfType<OperationTelemetry>();

            return  FilterOperationTelemetry(operationTelemetryList).ToList().CorrelationSort();
        }

        IEnumerable<OperationTelemetry> FilterOperationTelemetry(IEnumerable<OperationTelemetry> operationTelemetries)
        {
            return operationTelemetries.Where(
                p => p.Name.Contains(TraceMessages.Activity) || p.Name.Contains(TraceMessages.Orchestrator) || p.Name.Contains(TraceMessages.Client) || p.Name.Contains("Operation"));
        }

        async Task ExtractTelemetry(Type orchestrationType, string parameter, int timeout, ConcurrentQueue<ITelemetry> sendItems)
        {
            var sendAction = new Action<ITelemetry>(
                delegate(ITelemetry telemetry) { sendItems.Enqueue(telemetry); });
            new TelemetryActivator().Initialize(sendAction, Guid.NewGuid().ToString());
            // new TelemetryActivator().Initialize(item => sendItems.Enqueue(item), Guid.NewGuid().ToString());
            using (TestOrchestrationHost host = TestHelpers.GetTestOrchestrationHost(false))
            {
                await host.StartAsync();
                var activity = new Activity(TraceMessages.Client);
                activity.GenerateW3CContext();
                activity.Start();
                TestOrchestrationClient client = await host.StartOrchestrationAsync(orchestrationType, parameter);
                await client.WaitForCompletionAsync(TimeSpan.FromSeconds(timeout));

                await host.StopAsync();
            }
        }

        List<ITelemetry> ConvertTo(ConcurrentQueue<ITelemetry> queue)
        {
            var converted = new List<ITelemetry>();
            while (!queue.IsEmpty)
            {
                ITelemetry x;
                if (queue.TryDequeue(out x))
                {
                    converted.Add(x);
                }
            }
            return converted;
        }



    }
}
