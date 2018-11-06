using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurableTask.Core.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TraceContextTest
    {
        [TestMethod]
        public void GetRootId()
        {
            var context = new TraceContext()
            {
                Traceparent = "9761b85f886e3d48aa2f69163e83b195.faf02e91dab87340"
            };
            Assert.AreEqual("9761b85f886e3d48aa2f69163e83b195", context.RootId);
        }
    }
}
