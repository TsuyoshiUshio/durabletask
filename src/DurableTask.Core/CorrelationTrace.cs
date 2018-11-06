using System;
using System.Collections.Generic;
using System.Text;

namespace DurableTask.AzureStorage
{
    using DurableTask.Core;

    /// <summary>
    /// Correlation Trace for Telemetry
    /// </summary>
    public class CorrelationTrace
    {
        /// <summary>
        /// TraceContext 
        /// </summary>
        public TraceContext TraceContext { get; set; }

        /// <summary>
        /// Operation name of the Telemetry
        /// </summary>
        public string OperationName { get; set; }
    }
}
