using System;
using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Create a new instance of the DD logger with optional enviornment configuration.
    /// </summary>
    public class DDLoggerFactory : IDDLoggerFactory
    {
        private readonly ILambdaLogger _lambdaLogger;
        private readonly LoggingLevel _level;

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLoggerFactory"/> class.
        /// </summary>
        /// <param name="lambdaLogger">AWS Lambda logger.</param>
        /// <param name="level">Optional logging level.</param>
        public DDLoggerFactory(ILambdaLogger lambdaLogger, LoggingLevel level = LoggingLevel.Error)
        {
            string envLevelString = Environment.GetEnvironmentVariable("DD_LOG_LEVEL");

            if (!string.IsNullOrEmpty(envLevelString) && envLevelString.ToUpper() == "DEBUG")
            {
                switch (envLevelString.ToUpper())
                {
                    case "DEBUG":
                        _level = LoggingLevel.Debug;
                        break;
                    case "ERROR":
                        _level = LoggingLevel.Error;
                        break;
                }
            }
            else
            {
                _level = level;
            }

            _lambdaLogger = lambdaLogger;
        }

        /// <summary>
        /// Get a new instance of the logger.
        /// </summary>
        /// <returns>logger.</returns>
        public IDDLogger GetLogger()
        {
            return new DDLogger(_lambdaLogger, _level);
        }
    }
}
