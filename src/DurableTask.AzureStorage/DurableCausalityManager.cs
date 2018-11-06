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

namespace DurableTask.AzureStorage
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Tracks causality via queue<see cref="MessageData"/> with Activity. You can find causality releated method on <see cref="MessageDataExtensions"/>
    /// </summary>
    public class DurableCausalityManager
    {
        public const string W3CTraceId = "w3c_traceId";
        public const string W3CSpanId  = "w3c_spanId";
        public const string W3CTraceState = "w3c_tracestate";
    }
}
