using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Implementation of IDDLogger that logs to DataDog.
    /// </summary>
    internal class DDLogger : IDDLogger
    {
        private static ILambdaLogger _lambdaLogger;
        private LoggingLevel _level;

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLogger"/> class.
        /// </summary>
        /// <param name="lambdaLogger">AWS Lmabda logger.</param>
        /// <param name="level">logging level.</param>
        internal DDLogger(ILambdaLogger lambdaLogger, LoggingLevel level)
        {
            _lambdaLogger = lambdaLogger;
            _level = level;
        }

        /// <inheritdoc/>
        public void Debug(string message, params string[] tags)
        {
            if (_level == LoggingLevel.Debug)
            {
                DoLog(LoggingLevel.Debug, message, tags);
            }
        }

        /// <inheritdoc/>
        public void Error(string message, params string[] tags)
        {
            DoLog(LoggingLevel.Error, message, tags);
        }

        /// <inheritdoc/>
        public void SetLevel(LoggingLevel level)
        {
            _level = level;
        }

        private void DoLog(LoggingLevel level, string message, string[] tags)
        {
            StringBuilder argsSB = new StringBuilder("datadog: ");
            argsSB.Append(message);
            if (tags != null)
            {
                foreach (object a in tags)
                {
                    argsSB.Append(" ");
                    argsSB.Append(a);
                }
            }

            Dictionary<string, string> structuredLog = new Dictionary<string, string>();
            structuredLog.Add("level", level.ToString());
            structuredLog.Add("message", argsSB.ToString());

            _lambdaLogger.LogLine(JsonSerializer.Serialize(structuredLog));
        }
    }
}