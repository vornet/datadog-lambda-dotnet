using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    public class DDLogger
    {
        public enum level
        {
            DEBUG,
            ERROR
        }

        private static level? _gLevel;
        private static ILambdaLogger _lambdaLogger;
        private level _lLevel;

        public static DDLogger GetLoggerImpl(ILambdaLogger lambdaLogger)
        {
            _lambdaLogger = lambdaLogger;

            if (_gLevel != null) return new DDLogger();

            string env_level = Environment.GetEnvironmentVariable("DD_LOG_LEVEL");
            if (env_level == null) env_level = level.ERROR.ToString();

            if (env_level.ToUpper() == level.DEBUG.ToString())
            {
                _gLevel = level.DEBUG;
            }
            else
            {
                _gLevel = level.ERROR;
            }

            return new DDLogger();
        }

        private DDLogger()
        {
            _lLevel = _gLevel.Value;
        }

        public void Debug(string logMessage, params object[] args)
        {
            if (_lLevel == level.DEBUG)
            {
                DoLog(level.DEBUG, logMessage, args);
            }
        }

        public void Error(string logMessage, params object[] args)
        {
            DoLog(level.ERROR, logMessage, args);
        }

        private void DoLog(level l, string logMessage, object[] args)
        {
            StringBuilder argsSB = new StringBuilder("datadog: ");
            argsSB.Append(logMessage);
            if (args != null)
            {
                foreach(object a in args)
                {
                    argsSB.Append(" ");
                    argsSB.Append(a);
                }
            }

            Dictionary<string, string> structuredLog = new Dictionary<string, string>();
            structuredLog.Add("level", l.ToString());
            structuredLog.Add("message", argsSB.ToString());

            _lambdaLogger.LogLine(JsonSerializer.Serialize(structuredLog));
        }

        public void SetLevel(level l)
        {
            _lLevel = l;
        }
    }
}