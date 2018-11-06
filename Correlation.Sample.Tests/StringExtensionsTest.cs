using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Correlation.Sample.Tests
{
    using System;
    using Correlation.Samples;

    [TestClass]
    public class StringExtensionsTest
    {
        [TestMethod]
        public void TestParseTraceParent()
        {
            var traceparentString = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
            var traceparent = traceparentString.ToTraceparent();
            Assert.AreEqual("00", traceparent.Version);
            Assert.AreEqual("4bf92f3577b34da6a3ce929d0e0e4736", traceparent.TraceId);
            Assert.AreEqual("00f067aa0ba902b7", traceparent.SpanId);
            Assert.AreEqual("01", traceparent.TraceFlags);
        }

        [TestMethod]
        public void TestParseTraceParentThrowsException()
        {
            var wrongTraceparentString = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7";
            Assert.ThrowsException<ArgumentException>(
                () => { wrongTraceparentString.ToTraceparent(); });
        }

        [TestMethod]
        public void TestParseTraceParenWithNull()
        {
            string someString = null;
            var result = someString?.ToTraceparent();
            Assert.IsNull(result);
        }
    }
}
