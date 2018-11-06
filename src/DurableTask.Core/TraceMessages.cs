using System;
using System.Collections.Generic;
using System.Text;

namespace DurableTask.Core
{
    /// <summary>
    /// TraceMessages for Distributed Tracing
    /// </summary>
    public class TraceMessages
    {
        /// <summary>
        /// Client is the Distributed Tracing message for OrchestratorClient.
        /// </summary>
        public const string Client = "DtClient";

        /// <summary>
        /// Orchestrator is the Distributed Tracing message for Orchestrator.
        /// </summary>
        public const string Orchestrator = "DtOrchestrator";

        /// <summary>
        /// Activity is the Distributed Tracing message for Activity
        /// </summary>
        public const string Activity = "DtActivity";

    }
}
