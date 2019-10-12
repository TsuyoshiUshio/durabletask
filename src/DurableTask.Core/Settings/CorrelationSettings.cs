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

namespace DurableTask.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Text;

    /// <summary>
    /// Settings for Distributed Tracing
    /// </summary>
    public class CorrelationSettings
    {
        /// <summary>
        /// Create a new instance of the CorrelationSettings with default settings
        /// </summary>
        public CorrelationSettings()
        {
            Protocol = FrameworkConstants.CorrelationProtocolW3CTraceContext;
        }

        /// <summary>
        /// Correlation Protocol
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// Suppress Distributed Tracing
        /// default: true
        /// </summary>
        public bool DisablePropagation { get; set; } = true;

        /// <summary>
        /// Current Correlation Settings
        /// TODO Need to discuss the design for referencing Settings from DurableTask.Core side.
        /// </summary>
        public static CorrelationSettings Current { get; set; } = new CorrelationSettings();
    }
}
