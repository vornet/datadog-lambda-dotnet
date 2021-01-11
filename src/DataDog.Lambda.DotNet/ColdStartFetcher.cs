using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Get the cold start status.
    /// </summary>
    public class ColdStartFetcher
    {
        private static object _lock = new object();
        private static string _coldRequestId;

        /// <summary>
        /// Gets the cold_start status. The very first request to be made against this Lambda should be considered cold.  All others are warm.
        /// </summary>
        /// <param name="logger">The DD logger.</param>
        /// <param name="context">The AWS Lambda context.</param>
        /// <returns>true on the very first invocation, false otherwise.</returns>
        public static bool GetColdStart(IDDLogger logger, ILambdaContext context)
        {
            if (logger is null)
            {
                throw new System.ArgumentNullException(nameof(logger));
            }

            lock (_lock)
            {
                if (context == null)
                {
                    logger.Debug("Unable to determine cold_start: context was null");
                    return false;
                }

                string reqId = context.AwsRequestId;
                if (_coldRequestId == null)
                {
                    _coldRequestId = reqId;
                    return true;
                }

                if (_coldRequestId == reqId)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Resets cold start.
        /// </summary>
        public static void ResetColdStart()
        {
            lock (_lock)
            {
                _coldRequestId = null;
            }
        }
    }
}
