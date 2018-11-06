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

namespace DurableTask.Core
{
    using System;
    using System.Collections.Generic;
    using System.Security;
    using System.Text;
    using System.Threading;
    using DurableTask.Core;
#if NET451
    using System.Runtime.Remoting;
    using System.Runtime.Remoting.Messaging;

#endif

    /// <summary>
    /// Manage TraceContextBase for Dependency.
    /// This class share the TraceContextBase using AsyncLocal.
    /// </summary>
    public class CorrelationTraceContext
    {

#if NETSTANDARD2_0
        static AsyncLocal<TraceContextBase> current = new AsyncLocal<TraceContextBase>();
        static AsyncLocal<bool> suppressDependencyTracking = new AsyncLocal<bool>();
        /// <summary>
        /// Share the TraceContextBase on the call graph contextBase.
        /// </summary>
        public static TraceContextBase Current
        {
            get { return current.Value; }
            set { current.Value = value; }
        }

        /// <summary>
        /// Set true if the DependencyTelemetry tracking is not required on the TaskHubQueue.
        /// </summary>
        public static bool SuppressDependencyTracking
        {
            get { return suppressDependencyTracking.Value; }
            set { suppressDependencyTracking.Value = value; }
        }
#else

        const string TraceContextCurrentInstance = "TraceContextCurrentInstance";
        const string DependencyTelemetryHasTracked = "DependencyTelemetryHasTracked";

        /// <summary>
        /// Share the TraceContextBase on the call graph contextBase.
        /// </summary>
        public static TraceContextBase Current
        {
            [SecuritySafeCritical]
            get
            {
                var data = (ObjectHandle) CallContext.LogicalGetData(TraceContextCurrentInstance);
                if (data != null)
                {
                    return (TraceContextBase) data.Unwrap();                    
                }

                return (TraceContextBase) null;
            }
            [SecuritySafeCritical]
            set
            {
                CallContext.LogicalSetData(TraceContextCurrentInstance, (object)new ObjectHandle((object)value));
            }
        }

        /// <summary>
        /// It is true if the DependencyTelemetry has already tracked.
        /// </summary>
        public static bool SuppressDependencyTracking
        {
            [SecuritySafeCritical]
            get
            {
                ObjectHandle data = (ObjectHandle)CallContext.LogicalGetData(DependencyTelemetryHasTracked);
                if (data != null)
                {
                    return (bool)data.Unwrap();
                }

                return false;
            }
            [SecuritySafeCritical]
            set
            {
                CallContext.LogicalSetData(DependencyTelemetryHasTracked, (object)new ObjectHandle((object)value));
            }
        }
#endif
    }
}
