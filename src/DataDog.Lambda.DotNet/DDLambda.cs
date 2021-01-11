using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// An implementation of both enchanced metrics and X-ray tracking for AWS Lambda.
    /// </summary>
    public class DDLambda : IDDLambdaMetricsSender, IDDLambdaXrayTracer
    {
        private const string EnhancedEnv = "DD_ENHANCED_METRICS";
        private const string EnhancedPrefix = "aws.lambda.enhanced.";
        private const string Invocation = "invocations";
        private const string ErrorName = "errors";

        private readonly ILambdaContext _context;
        private readonly IDDLogger _logger;
        private readonly IMetricWriter _metricsWriter;

        private XRayTracer _tracer;
        private bool _enhanced = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLambda"/> class with just the Lambda context.
        /// </summary>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        public DDLambda(ILambdaContext context)
        {
            _context = context;
            _logger = new DDLoggerFactory(context.Logger).GetLogger();
            _metricsWriter = new LambdaLogMetricWriter(context.Logger);
            _tracer = new XRayTracer(_logger);
            _enhanced = CheckEnhanced();

            RecordEnhanced(Invocation);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLambda"/> class with Lambda context and a logger factory.
        /// </summary>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        public DDLambda(ILambdaContext context, IDDLoggerFactory loggerFactory)
        {
            _context = context;
            _logger = loggerFactory.GetLogger();
            _metricsWriter = new LambdaLogMetricWriter(context.Logger);
            _tracer = new XRayTracer(_logger);
            _enhanced = CheckEnhanced();

            RecordEnhanced(Invocation);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLambda"/> class with Lambda context and logger.
        /// </summary>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        /// <param name="logger">DD logger.</param>
        /// <param name="metricWriter">Metrics writer.</param>
        public DDLambda(ILambdaContext context, IDDLogger logger, IMetricWriter metricWriter)
        {
            _context = context;
            _logger = logger;
            _metricsWriter = metricWriter;
            _tracer = new XRayTracer(logger);
            _enhanced = CheckEnhanced();

            RecordEnhanced(Invocation);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLambda"/> class with an API Gateway proxy request.
        /// </summary>
        /// <param name="req">Your Datadog trace headers are pulled from the request and sent to XRay for consumption by the Datadog Xray Crawler.</param>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        public DDLambda(ILambdaContext context, APIGatewayProxyRequest req)
        {
            _context = context;
            _enhanced = CheckEnhanced();
            RecordEnhanced(Invocation);

            _tracer = new XRayTracer(_logger, req);
            _ = _tracer.SubmitSegmentAsync().Result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLambda"/> class.
        /// Create a trace-enabled DDLambda instrumenter given a custom request object.
        /// <remarks>Please note that your custom request object MUST implement Headerable.</remarks>
        /// </summary>
        /// <param name="req">A custom request object that implements Headerable. Datadog trace headers are pulled from this request object.</param>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        public DDLambda(ILambdaContext context, IHeaderable req)
        {
            _context = context;
            _enhanced = CheckEnhanced();
            RecordEnhanced(Invocation);
            _tracer = new XRayTracer(_logger, req);
            _ = _tracer.SubmitSegmentAsync().Result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DDLambda"/> class with an X-Ray trace information.
        /// </summary>
        /// <param name="context">Enhanced Metrics pulls information from the Lambda context.</param>
        /// <param name="xrayTraceInfo">This would normally be the contents of the "_X_AMZN_TRACE_ID" env var.</param>
        internal DDLambda(ILambdaContext context, string xrayTraceInfo)
        {
            _context = context;
            _tracer = new XRayTracer(_logger, xrayTraceInfo);
            _enhanced = CheckEnhanced();

            RecordEnhanced(Invocation);
        }

        /// <inheritdoc/>
        public void SendCustom(string name, double value, Dictionary<string, string> tags)
        {
            _metricsWriter.Write(new CustomMetric(name, value, tags, DateTimeOffset.UtcNow));
        }

        /// <inheritdoc/>
        public void SendCustom(string name, double value, Dictionary<string, string> tags, DateTimeOffset date)
        {
            _metricsWriter.Write(new CustomMetric(name, value, tags, date));
        }

        /// <inheritdoc/>
        public void IncrementError()
        {
            RecordEnhanced(ErrorName);
        }

        /// <inheritdoc/>
        public void AddTraceHeaders(ref HttpRequestMessage httpRequestMessage)
        {
            if (_tracer == null)
            {
                _logger.Error("Unable to add trace headers from an untraceable request. Did you pass LambdaInstrumenter a request?");
                return;
            }

            foreach (var traceHeader in _tracer.MakeOutboundHttpTraceHeaders())
            {
                httpRequestMessage.Headers.Add(traceHeader.Key, traceHeader.Value);
            }
        }

        /// <summary>
        /// Add Datadog trace headers to the passed in HttpClient's default headers.
        /// </summary>
        /// <param name="httpClient">HTTP client to decorate.</param>
        public void AddTraceHeaders(ref HttpClient httpClient)
        {
            if (_tracer == null)
            {
                _logger.Error("Unable to add trace headers from an untraceable request. Did you pass LambdaInstrumenter a request?");
                return;
            }

            Dictionary<string, string> traceHeaders = _tracer.MakeOutboundHttpTraceHeaders();

            foreach (var traceHeader in traceHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(traceHeader.Key, traceHeader.Value);
            }
        }

        /// <summary>
        /// Get the trace context for trace/log correlation. Inject this into your logs in order to correlate logs with traces.
        /// </summary>
        /// <returns>a map of the current trace context.</returns>
        public Dictionary<string, string> GetTraceContext()
        {
            if (_tracer == null)
            {
                _logger.Debug("No tracing context; unable to get Trace ID");
                return null;
            }

            return _tracer.GetLogCorrelationTraceAndSpanIDsMap();
        }

        /// <summary>
        /// Get the trace context in string form. Inject this into your logs in order to correlate logs with traces.
        /// </summary>
        /// <returns>a string representation of the current trace context.</returns>
        public string GetTraceContextString()
        {
            Dictionary<string, string> traceInfo = GetTraceContext();

            if (traceInfo == null)
            {
                _logger.Debug("No Trace/Log correlation IDs returned");
                return string.Empty;
            }

            string traceId = traceInfo[XRayTracer.TraceIdKey];
            string spanId = traceInfo[XRayTracer.SpanIdKey];

            return FormatTraceContext(XRayTracer.TraceIdKey, traceId, XRayTracer.SpanIdKey, spanId);
        }

        private void RecordEnhanced(string basename)
        {
            string metricName = basename;

            Dictionary<string, string> tags = null;
            if (_enhanced)
            {
                metricName = EnhancedPrefix + basename;
                tags = MakeTagsFromContext();
            }

            _metricsWriter.Write(new CustomMetric(metricName, 1, tags, DateTimeOffset.UtcNow));
        }

        private Dictionary<string, string> MakeTagsFromContext()
        {
            Dictionary<string, string> tags = new Dictionary<string, string>();
            if (_context != null && _context.InvokedFunctionArn != null)
            {
                string[] arnParts = _context.InvokedFunctionArn.Split(':');
                string region = string.Empty;
                string accountId = string.Empty;
                string alias = string.Empty;

                if (arnParts.Length > 3)
                {
                    region = arnParts[3];
                }

                if (arnParts.Length > 4)
                {
                    accountId = arnParts[4];
                }

                if (arnParts.Length > 7)
                {
                    alias = arnParts[7];
                }

                if (!string.IsNullOrWhiteSpace(alias))
                {
                    // Drop $ from tag if it is $Latest
                    if (alias.StartsWith("$"))
                    {
                        alias = alias.Substring(1);
                    }
                    else if (!alias.All(char.IsNumber))
                    {
                        // Make sure it is an alias and not a number
                        tags.Add("executedversion", _context.FunctionVersion);
                    }

                    tags.Add("resource", _context.FunctionName + ":" + alias);
                }
                else
                {
                    tags.Add("resource", _context.FunctionName);
                }

                tags.Add("functionname", _context.FunctionName);
                tags.Add("region", region);
                tags.Add("account_id", accountId);
                tags.Add("memorysize", _context.MemoryLimitInMB.ToString());
                tags.Add("cold_start", ColdStartFetcher.GetColdStart(_logger, _context).ToString());
                tags.Add("datadog_lambda", Assembly.GetEntryAssembly().GetName().Version.ToString());
            }
            else
            {
                _logger.Debug("Unable to enhance metrics: context was null.");
            }

            string runtime = ".NET " + Environment.Version;
            tags.Add("runtime", runtime);

            return tags;
        }

        private bool CheckEnhanced()
        {
            string sysEnhanced = Environment.GetEnvironmentVariable(EnhancedEnv);

            if (sysEnhanced != null && sysEnhanced.ToLower() == "false")
            {
                return false;
            }

            return true;
        }

        private string FormatTraceContext(string traceKey, string trace, string spanKey, string span)
        {
            return $"[{traceKey}={trace} {spanKey}={span}]";
        }
    }
}