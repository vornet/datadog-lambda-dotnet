using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DataDog.Lambda.DotNet
{
    public class EnhancedMetric
    {
        public static Dictionary<string, object> MakeTagsFromContext(ILambdaContext context)
        {
            Dictionary<string, object> tags = new Dictionary<string, object>();
            if (context != null && context.InvokedFunctionArn != null)
            {
                string[] arnParts = context.InvokedFunctionArn.Split(':');
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
                        tags.Add("executedversion", context.FunctionVersion);
                    }
                    tags.Add("resource", context.FunctionName + ":" + alias);
                }
                else
                {
                    tags.Add("resource", context.FunctionName);
                }
                tags.Add("functionname", context.FunctionName);
                tags.Add("region", region);
                tags.Add("account_id", accountId);
                tags.Add("memorysize", context.MemoryLimitInMB);
                tags.Add("cold_start", ColdStart.GetColdStart(context));
                tags.Add("datadog_lambda", Assembly.GetEntryAssembly().GetName().Version);
            }
            else
            {
                DDLogger.GetLoggerImpl(context.Logger).Debug("Unable to enhance metrics: context was null.");
            }
            string runtime = ".NET " + Environment.Version;
            tags.Add("runtime", runtime);
            return tags;
        }
    }
}