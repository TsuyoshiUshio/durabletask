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
    using Newtonsoft.Json;

    [TestClass]
    public class CorrelationScenarioTest
    {
        [TestMethod]
        public async Task SingleOrchestratorWithSingleActivityAsync()
        {
            var host = new TestCorrelationOrchestrationHost();
            List<OperationTelemetry> actual = await host.ExecuteOrchestrationAsync(typeof(SayHelloActivity), "world", 360);
            Assert.AreEqual(6, actual.Count);
            Assert.AreEqual(TraceConstants.Client, actual[0].Name); // Start Orchestration
            Assert.AreEqual(TraceConstants.Client, actual[1].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} SayHelloActivity", actual[2].Name); // Orchestrator start
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[3].Name); // Activity Start
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[4].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} SayHelloActivity", actual[5].Name); // Orchestrator Stop
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
            var host = new TestCorrelationOrchestrationHost();
            // parameter = null cause an exception. 
            Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>> result = await host.ExecuteOrchestrationWithExceptionAsync(typeof(SayHelloActivity), null, 50);

            List<OperationTelemetry> actual = result.Item1;
            List<ExceptionTelemetry> actualExceptions = result.Item2;

            Assert.AreEqual(6, actual.Count);
            var index = 0;
            Assert.AreEqual(TraceConstants.Client, actual[index].Name); // Start Orchestration Request
            Assert.AreEqual(TraceConstants.Client, actual[++index].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} SayHelloActivity", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} SayHelloActivity", actual[++index].Name); // Orchestrator Dependency            

            Assert.AreEqual(actualExceptions[0].Context.Operation.ParentId, actual[3].Id);
            Assert.AreEqual(actualExceptions[1].Context.Operation.ParentId, actual[2].Id);
        }

        [TestMethod]
        public async Task SingleOrchestratorWithMultipleActivitiesAsync()
        {
            var host = new TestCorrelationOrchestrationHost();
            List<OperationTelemetry> actual = await host.ExecuteOrchestrationAsync(typeof(SayHelloActivities), "world", 50);
            Assert.AreEqual(8, actual.Count);

            // Client Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[0].GetType().ToString());
            Assert.AreEqual(TraceConstants.Client, actual[0].Name);
            // Client Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[1].GetType().ToString());
            Assert.AreEqual(TraceConstants.Client, actual[1].Name);
            // Orchestrator Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[2].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} SayHelloActivities", actual[2].Name);
            // Activity 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[3].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} HelloWait", actual[3].Name);
            // Activity 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[4].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} HelloWait", actual[4].Name);
            // Activity 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[5].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} HelloWait", actual[5].Name);
            // Activity 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[6].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} HelloWait", actual[6].Name);
            // Orchestrator Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[7].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} SayHelloActivities", actual[7].Name);
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
            var host = new TestCorrelationOrchestrationHost();
            List<OperationTelemetry> actual = await host.ExecuteOrchestrationAsync(typeof(ParentOrchestrator), "world", 50);
            Assert.AreEqual(8, actual.Count);
            // Client Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[0].GetType().ToString());
            Assert.AreEqual(TraceConstants.Client, actual[0].Name);
            // Client Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[1].GetType().ToString());
            Assert.AreEqual(TraceConstants.Client, actual[1].Name);
            // Orchestrator Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[2].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ParentOrchestrator", actual[2].Name);
            // Activity 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[3].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestrator", actual[3].Name);
            // Activity 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[4].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[4].Name);
            // Activity 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[5].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[5].Name);
            // Activity 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[6].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestrator", actual[6].Name);
            // Orchestrator Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[7].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ParentOrchestrator", actual[7].Name);
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
            var host = new TestCorrelationOrchestrationHost();
            List<OperationTelemetry> actual = await host.ExecuteOrchestrationAsync(typeof(ParentOrchestratorWithMultiLayeredSubOrchestrator), "world", 50);
            Assert.AreEqual(14, actual.Count);
            // Client Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[0].GetType().ToString());
            Assert.AreEqual(TraceConstants.Client, actual[0].Name);
            // Client Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[1].GetType().ToString());
            Assert.AreEqual(TraceConstants.Client, actual[1].Name);
            // Parent Orchestrator Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[2].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ParentOrchestratorWithMultiLayeredSubOrchestrator", actual[2].Name);
            // Child Orchestrator Level 1 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[3].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestratorWithMultiSubOrchestrator", actual[3].Name);
            // Child Orchestrator Level 2 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[4].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestrator", actual[4].Name);
            // Activity 1 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[5].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[5].Name);
            // Activity 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[6].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[6].Name);
            // Child Orchestrator Level 2 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[7].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestrator", actual[7].Name);
            // Child Orchestrator Level 2 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[8].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestrator", actual[8].Name);
            // Activity 2 Request
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.RequestTelemetry", actual[9].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[9].Name);
            // Activity 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[10].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[10].Name);
            // Child Orchestrator Level 2 2 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[11].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestrator", actual[11].Name);
            // Child Orchestrator Level 1 1 Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[12].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ChildOrchestratorWithMultiSubOrchestrator", actual[12].Name);
            // Orchestrator Dependency
            Assert.AreEqual("Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry", actual[13].GetType().ToString());
            Assert.AreEqual($"{TraceConstants.Orchestrator} ParentOrchestratorWithMultiLayeredSubOrchestrator", actual[13].Name);
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
            var host = new TestCorrelationOrchestrationHost();
            Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>> resultTuple = await host.ExecuteOrchestrationWithExceptionAsync(typeof(SingleOrchestrationWithRetry), "world", 50);
            List<OperationTelemetry> actual = resultTuple.Item1;
            List<ExceptionTelemetry> actualExceptions = resultTuple.Item2;
            Assert.AreEqual(8, actual.Count);
            Assert.AreEqual(TraceConstants.Client, actual[0].Name); // Start Orchestration
            Assert.AreEqual(TraceConstants.Client, actual[1].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} SingleOrchestrationWithRetry", actual[2].Name); // Orchestrator start
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice", actual[3].Name); // Activity Start
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice", actual[4].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice", actual[5].Name); // Activity Start
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice", actual[6].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} SingleOrchestrationWithRetry", actual[7].Name); // Orchestrator Stop

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
            var host = new TestCorrelationOrchestrationHost();
            Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>> resultTuple = await host.ExecuteOrchestrationWithExceptionAsync(typeof(MultiLayeredOrchestrationWithRetry), "world", 50);
            List<OperationTelemetry> actual = resultTuple.Item1;
            List<ExceptionTelemetry> actualExceptions = resultTuple.Item2;
            Assert.AreEqual(20, actual.Count);
            var index = 0;
            Assert.AreEqual(TraceConstants.Client, actual[index].Name); // Start Orchestration Request
            Assert.AreEqual(TraceConstants.Client, actual[++index].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationWithRetry", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Dependency 
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Request Retry
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish Request
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Dependency Retry
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator Request Retry
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice01", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish Request
            Assert.AreEqual($"{TraceConstants.Activity} NeedToExecuteTwice02", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationChildWithRetry", actual[++index].Name); // Orchestrator dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiLayeredOrchestrationWithRetry", actual[++index].Name); // Orchestrator dependency

            Assert.AreEqual(actualExceptions[0].Context.Operation.ParentId, actual[4].Id);
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

        [TestMethod]
        public async Task ContinueAsNewAsync()
        {
            var host = new TestCorrelationOrchestrationHost();
            List<OperationTelemetry> actual = await host.ExecuteOrchestrationAsync(typeof(ContinueAsNewOrchestration), "world", 50);
            Assert.AreEqual(12, actual.Count);
            var index = 0;
            Assert.AreEqual(TraceConstants.Client, actual[index].Name); // Start Orchestration Request
            Assert.AreEqual(TraceConstants.Client, actual[++index].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} ContinueAsNewOrchestration", actual[++index].Name); // Orchestrator Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Dependency
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Dependency
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Dependency
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity Request
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[++index].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} ContinueAsNewOrchestration", actual[++index].Name); // Orchestrator Dependency
        }

        [KnownType(typeof(Hello))]
        internal class ContinueAsNewOrchestration : TaskOrchestration<string, string>
        {
            static int counter = 0;

            public override async Task<string> RunTask(OrchestrationContext context, string input)
            {
                string result = await context.ScheduleTask<string>(typeof(Hello), input);
                result = input + ":" + result;
                if (counter < 3)
                {
                    counter++;
                    context.ContinueAsNew(result);
                }

                return result;
            }
        }

        [TestMethod]
        public async Task MultipleParentScenarioAsync()
        {
            var host = new TestCorrelationOrchestrationHost();
            var tasks = new List<Task>();
            tasks.Add(host.ExecuteOrchestrationAsync(typeof(MultiParentOrchestrator), "world", 30));

            while (IsNotReadyForRaiseEvent(host.Client))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300));
            }

            tasks.Add(host.Client.RaiseEventAsync("someEvent", "hi"));
            await Task.WhenAll(tasks);

            List<OperationTelemetry> actual = Convert(tasks[0]);

            Assert.AreEqual(6, actual.Count);
            Assert.AreEqual(TraceConstants.Client, actual[0].Name); // Start Orchestration
            Assert.AreEqual(TraceConstants.Client, actual[1].Name); // Start Orchestration Dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiParentOrchestrator", actual[2].Name); // Orchestrator start
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[3].Name); // Activity Start
            Assert.AreEqual($"{TraceConstants.Activity} Hello", actual[4].Name); // Activity finish dependency
            Assert.AreEqual($"{TraceConstants.Orchestrator} MultiParentOrchestrator", actual[5].Name); // Orchestrator Stop
        }

        bool IsNotReadyForRaiseEvent(TestOrchestrationClient client)
        {
            return client == null && !MultiParentOrchestrator.IsWaitForExternalEvent;
        }

        List<OperationTelemetry> Convert(Task task)
        {
            return (task as Task<List<OperationTelemetry>>)?.Result;
        }

        [KnownType(typeof(Hello))]
        internal class MultiParentOrchestrator : TaskOrchestration<string, string>
        {
            public static bool IsWaitForExternalEvent { get; set; } = false;

            readonly TaskCompletionSource<object> receiveEvent = new TaskCompletionSource<object>();

            public async override Task<string> RunTask(OrchestrationContext context, string input)
            {
                IsWaitForExternalEvent = true;
                await this.receiveEvent.Task;
                await context.ScheduleTask<string>(typeof(Hello), input);
                return "done";
            }

            public override void OnEvent(OrchestrationContext context, string name, string input)
            {
                this.receiveEvent.SetResult(null);
            }
        }

        //[TestMethod] terminate

        class TestCorrelationOrchestrationHost
        {
            internal TestOrchestrationClient Client { get; set; }

            internal async Task<Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>>> ExecuteOrchestrationWithExceptionAsync(Type orchestrationType, string parameter, int timeout)
            {
                var sendItems = new ConcurrentQueue<ITelemetry>();
                await ExtractTelemetry(orchestrationType, parameter, timeout, sendItems);

                var sendItemList = ConvertTo(sendItems);
                var operationTelemetryList = sendItemList.OfType<OperationTelemetry>();
                var exceptionTelemetryList = sendItemList.OfType<ExceptionTelemetry>().ToList();

                List<OperationTelemetry> operationTelemetries = FilterOperationTelemetry(operationTelemetryList).ToList().CorrelationSort();

                return new Tuple<List<OperationTelemetry>, List<ExceptionTelemetry>>(operationTelemetries, exceptionTelemetryList);
            }

            internal async Task<List<OperationTelemetry>> ExecuteOrchestrationAsync(Type orchestrationType, string parameter, int timeout)
            {
                var sendItems = new ConcurrentQueue<ITelemetry>();
                await ExtractTelemetry(orchestrationType, parameter, timeout, sendItems);

                var sendItemList = ConvertTo(sendItems);
                var operationTelemetryList = sendItemList.OfType<OperationTelemetry>();

                var result = FilterOperationTelemetry(operationTelemetryList).ToList();
                Debug.WriteLine(
                    JsonConvert.SerializeObject(
                        result.Select(
                            x => new
                            {
                                Type = x.GetType().Name,
                                OperationName = x.Name,
                                Id = x.Id,
                                OperationId = x.Context.Operation.Id,
                                OperationParentId = x.Context.Operation.ParentId,
                            })));

                return result.CorrelationSort();
            }

            IEnumerable<OperationTelemetry> FilterOperationTelemetry(IEnumerable<OperationTelemetry> operationTelemetries)
            {
                return operationTelemetries.Where(
                    p => p.Name.Contains(TraceConstants.Activity) || p.Name.Contains(TraceConstants.Orchestrator) || p.Name.Contains(TraceConstants.Client) || p.Name.Contains("Operation"));
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
                    var activity = new Activity(TraceConstants.Client);
#pragma warning disable 618
                    activity.GenerateW3CContext();
#pragma warning restore 618
                    activity.Start();
                    Client = await host.StartOrchestrationAsync(orchestrationType, parameter);
                    await Client.WaitForCompletionAsync(TimeSpan.FromSeconds(timeout));

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
}
