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

    public class Program
    {
        static void Main(string[] args)
        {
            // Add CI change with new config with debug. 
            // new HelloWorldScenario().ExecuteAsync().GetAwaiter().GetResult(); // basic sample
            new MultiLayerOrchestrationWithRetryScenario().ExecuteAsync().GetAwaiter().GetResult(); // complex sample
            Console.WriteLine("hello");
            Console.WriteLine("Orchestration is successfully finished.");
            Console.ReadLine();
        }
    }
}
