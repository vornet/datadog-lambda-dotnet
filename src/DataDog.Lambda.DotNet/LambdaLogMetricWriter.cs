using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Write metrics through the Lambda logger.
    /// </summary>
    internal class LambdaLogMetricWriter : IMetricWriter
    {
        private readonly ILambdaLogger _lambdaLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaLogMetricWriter"/> class.
        /// </summary>
        /// <param name="lambdaLogger">AWS Lambda logger.</param>
        public LambdaLogMetricWriter(ILambdaLogger lambdaLogger)
        {
            _lambdaLogger = lambdaLogger;
        }

        /// <inheritdoc/>
        public void Write(CustomMetric cm)
        {
            _lambdaLogger.LogLine(cm.ToJsonString());
        }

        /// <inheritdoc/>
        public void Flush()
        {
        }
    }
}
