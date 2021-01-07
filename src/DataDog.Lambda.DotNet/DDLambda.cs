using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace DataDog.Lambda.DotNet
{
    public class DDLambda
    {
        public const string EnhancedEnv = "DD_ENHANCED_METRICS";
        public const string EnhancedPrefix = "aws.lambda.enhanced.";
        public const string Invocation = "invocations";
        private const string ErrorName = "errors";

        private Tracing _tracing;
        private bool _enhanced = true;

        /// <summary>
        /// Create a new DDLambda instrumenter given some Lambda context
        /// </summary>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        public DDLambda(ILambdaContext context)
        {
            _tracing = new Tracing();
            _enhanced = CheckEnhanced();
            RecordEnhanced(Invocation, context);
        }

        /// <summary>
        /// Testing only: create a DDLambda instrumenter with a given context and xrayTraceInfo
        /// </summary>
        /// <param name="cxt">Enhanced Metrics pulls information from the Lambda context.</param>
        /// <param name="xrayTraceInfo">This would normally be the contents of the "_X_AMZN_TRACE_ID" env var</param>
        public DDLambda(ILambdaContext cxt, string xrayTraceInfo)
        {
            _tracing = new Tracing(xrayTraceInfo);
            _enhanced = CheckEnhanced();
            RecordEnhanced(Invocation, cxt);
        }

        /// <summary>
        /// Create a trace-enabled DDLambda instrumenter given an APIGatewayProxyRequestEvent and a Lambda context         
        /// </summary>
        /// <param name="req">Your Datadog trace headers are pulled from the request and sent to XRay for consumption by the Datadog Xray Crawler</param>
        /// <param name="cxt">Enhanced Metrics pulls information from the Lambda context.</param>
        public DDLambda(APIGatewayProxyRequest req, ILambdaContext cxt)
        {
            _enhanced = CheckEnhanced();
            RecordEnhanced(Invocation, cxt);
            _tracing = new Tracing(req);
            _tracing.SubmitSegment();
        }

        
        /// <summary>
        /// Create a trace-enabled DDLambda instrumenter given a custom request object. 
        /// <remarks>Please note that your custom request object MUST implement Headerable.</remarks>
        /// </summary>
        /// <param name="req">A custom request object that implements Headerable. Datadog trace headers are pulled from this request object.</param>
        /// <param name="cxt">Enhanced Metrics pulls information from the Lambda context.</param>
        public DDLambda(IHeaderable req, ILambdaContext cxt)
        {
            _enhanced = CheckEnhanced();
            RecordEnhanced(Invocation, cxt);
            _tracing = new Tracing(req);
            _tracing.SubmitSegment();
        }

        protected bool CheckEnhanced()
        {
            string sysEnhanced = Environment.GetEnvironmentVariable(EnhancedEnv);
            if (sysEnhanced == null)
            {
                return true;
            }

            if (sysEnhanced.ToLower() == "false")
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Metric allows the user to record their own custom metric that will be sent to Datadog.
        /// </summary>
        /// <param name="name">The metric's name</param>
        /// <param name="value">The metric's value</param>
        /// <param name="tags">A map of tags to be assigned to the metric</param>
        public void Metric(string name, double value, Dictionary<string, object> tags)
        {
            new CustomMetric(name, value, tags).Write();
        }

        /// <summary>
        /// Metric allows the user to record their own custom metric that will be sent to Datadog.
        /// <remarks>also allows user to set his/her own date.</remarks>
        /// </summary>
        /// <param name="name">The metric's name</param>
        /// <param name="value">The metric's value</param>
        /// <param name="tags">A map of tags to be assigned to the metric</param>
        /// <param name="date">The date under which the metric value will appear in datadog</param>
        public void Metric(string name, double value, Dictionary<string, object> tags, DateTimeOffset date)
        {
            new CustomMetric(name, value, tags, date).Write();
        }

        /// <summary>
        /// Increments the aws.lambda.enhanced.error metric in Datadog.
        /// </summary>
        /// <param name="cxt">The AWS Context provided to your handler</param>
        public void Error(ILambdaContext cxt)
        {
            RecordEnhanced(ErrorName, cxt);
        }

        private void RecordEnhanced(string basename, ILambdaContext cxt)
        {
            string metricName = basename;
            Dictionary<string, object> tags = null;
            if (_enhanced)
            {
                metricName = EnhancedPrefix + basename;
                tags = EnhancedMetric.MakeTagsFromContext(cxt);
            }
            new CustomMetric(metricName, 1, tags).Write();
        }

        /// <summary>
        /// Adds Datadog trace headers to a HttpRequestMessage, so you can trace downstream HTTP requests.
        /// </summary>
        /// <param name="httpRequestMessage">HTTP request message to decorate.</param>
        public void AddTraceHeaders(ref HttpRequestMessage httpRequestMessage)
        {
            if (_tracing == null)
            {
                DDLogger.GetLoggerImpl().Error("Unable to add trace headers from an untraceable request. Did you pass LambdaInstrumenter a request?");
                return;
            }

            foreach (var traceHeader in _tracing.MakeOutboundHttpTraceHeaders())
            {
                httpRequestMessage.Headers.Add(traceHeader.Key, traceHeader.Value);
            }
        }

        /// <summary>
        /// Add Datadog trace headers to the passed in HttpClient's default headers.
        /// </summary>
        /// <param name="httpClient">HTTP client to decorate</param>
        public void AddTraceHeaders(ref HttpClient httpClient)
        {
            if (_tracing == null)
            {
                DDLogger.GetLoggerImpl().Error("Unable to add trace headers from an untraceable request. Did you pass LambdaInstrumenter a request?");
                return;
            }

            Dictionary<string, string> traceHeaders = _tracing.MakeOutboundHttpTraceHeaders();

            foreach (var traceHeader in traceHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(traceHeader.Key, traceHeader.Value);
            }
        }

        /// <summary>
        /// Get the trace context for trace/log correlation.Inject this into your logs in order to correlate logs with traces.
        /// </summary>
        /// <returns>a map of the current trace context</returns>
        public Dictionary<string, string> GetTraceContext()
        {
            if (_tracing == null)
            {
                DDLogger.GetLoggerImpl().Debug("No tracing context; unable to get Trace ID");
                return null;
            }
            return _tracing.GetLogCorrelationTraceAndSpanIDsMap();
        }

        /// <summary>
        /// Get the trace context in string form.Inject this into your logs in order to correlate logs with traces.
        /// </summary>
        /// <returns>a string representation of the current trace context</returns>
        public string GetTraceContextString()
        {
            Dictionary<string, string> traceInfo = GetTraceContext();
            if (traceInfo == null)
            {
                DDLogger.GetLoggerImpl().Debug("No Trace/Log correlation IDs returned");
                return "";
            }

            string traceID = traceInfo[Tracing.TraceIdKey];
            string spanID = traceInfo[Tracing.SpanIdKey];
            return FormatTraceContext(Tracing.TraceIdKey, traceID, Tracing.SpanIdKey, spanID);
        }

        private string FormatTraceContext(string traceKey, string trace, string spanKey, string span)
        {
            return $"[{traceKey}={trace} {spanKey}={span}]";
        }
    }
}