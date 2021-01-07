using System;

namespace DataDog.Lambda.DotNet
{
    abstract class MetricWriter
    {
        private static MetricWriter _impl;
        public static MetricWriter GetMetricWriterImpl()
        {
            lock (_impl)
            {
                if (_impl == null)
                {
                    // Potential to check for an env var and choose a different writer if we decide to support that
                    _impl = new StdoutMetricWriter();
                }
                return _impl;
            }
        }

        /// <summary>
        /// Gives you the ability to set the metrics writer, for testing purposes
        /// <param name="mw">the new Metrics Write implementation</param>
        public static void SetMetricWriter(MetricWriter mw)
        {
            _impl = mw;
        }

        public abstract void Write(CustomMetric cm);
        public abstract void Flush();
    }

    class StdoutMetricWriter : MetricWriter {

        override public void Write(CustomMetric cm)
        {
            Console.WriteLine(cm.ToJson());
        }

        override public void Flush() { }
    }
}
