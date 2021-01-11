namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Implementation should write metrics.
    /// </summary>
    public interface IMetricWriter
    {
        /// <summary>
        /// Write custom metric.
        /// </summary>
        /// <param name="customMetric">Custom metric.</param>
        void Write(CustomMetric customMetric);

        /// <summary>
        /// Flush any pending metrics in the buffer.
        /// </summary>
        void Flush();
    }
}
