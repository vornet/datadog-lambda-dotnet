using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DataDog.Lambda.DotNet
{
    public class EnhancedMetric
    {
        public static Dictionary<string, object> MakeTagsFromContext(ILambdaContext ctx)
        {
            Dictionary<string, object> m = new Dictionary<string, object>();
            if (ctx != null)
            {
                string[] arnParts = ctx.InvokedFunctionArn.Split(':');
                string region = "";
                string accountId = "";
                string alias = "";

                if (arnParts.Length > 3) region = arnParts[3];
                if (arnParts.Length > 4) accountId = arnParts[4];
                if (arnParts.Length > 7) alias = arnParts[7];

                if (!string.IsNullOrWhiteSpace(alias))
                {
                    // Drop $ from tag if it is $Latest
                    if (alias.StartsWith("$"))
                    {
                        alias = alias.Substring(1);
                        // Make sure it is an alias and not a number
                    }
                    else if (!alias.All(char.IsNumber))
                    {
                        m.Add("executedversion", ctx.FunctionVersion);
                    }
                    m.Add("resource", ctx.FunctionName + ":" + alias);
                }
                else
                {
                    m.Add("resource", ctx.FunctionName);
                }
                m.Add("functionname", ctx.FunctionName);
                m.Add("region", region);
                m.Add("account_id", accountId);
                m.Add("memorysize", ctx.MemoryLimitInMB);
                m.Add("cold_start", ColdStart.GetColdStart(ctx));
                m.Add("datadog_lambda", Assembly.GetEntryAssembly().GetName().Version);
            }
            else
            {
                DDLogger.GetLoggerImpl().Debug("Unable to enhance metrics: context was null.");
            }
            string runtime = ".NET " + Environment.Version;
            m.Add("runtime", runtime);
            return m;
        }
    }
}