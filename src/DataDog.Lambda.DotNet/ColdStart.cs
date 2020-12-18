using System;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;

namespace DataDog.Lambda.DotNet
{
    internal class ColdStart
    {
        static object _lock = new object();

        private static string _coldRequestId;

        /**
         * Gets the cold_start status. The very first request to be made against this Lambda should be considered cold.
         * All others are warm.
         * @return true on the very first invocation, false otherwise
         */
               
        public static bool GetColdStart(ILogger logger, ILambdaContext context)
        {
            lock (_lock)
            {
                if (context == null)
                {
                    logger.LogDebug("Unable to determine cold_start: context was null");
                    return false;
                }
                String reqId = context.AwsRequestId;
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

        public static void ResetColdStart()
        {
            lock (_lock)
            {
                _coldRequestId = null;
            }
        }
    }
}
