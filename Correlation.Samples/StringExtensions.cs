using System;
using System.Collections.Generic;
using System.Text;

namespace Correlation.Samples
{
    public static class StringExtensions
    {
        public static Traceparent ToTraceparent(this string traceparent)
        {
            if (!string.IsNullOrEmpty(traceparent))
            {
                var substrings = traceparent.Split('-');
                if (substrings.Length != 4)
                {
                    throw new ArgumentException($"Traceparent doesn't respect the spec. {traceparent}");
                }

                return new Traceparent
                {
                    Version = substrings[0],
                    TraceId = substrings[1],
                    SpanId = substrings[2],
                    TraceFlags = substrings[3]
                };
            }

            return null;
        }
    }

}
