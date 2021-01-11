using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using DataDog.Lambda.DotNet.Models.Xray;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Tracing for AWS XRay.
    /// </summary>
    public class XRayTracer
    {
        /// <summary>
        /// Trace id key.
        /// </summary>
        public const string TraceIdKey = "dd.trace_id";

        /// <summary>
        /// Span id key.
        /// </summary>
        public const string SpanIdKey = "dd.span_id";

        private IDDLogger _logger;
        private DDTraceContext _traceContext;
        private XRayTraceContext _xrayContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="XRayTracer"/> class.
        /// </summary>
        /// <param name="logger">DD logger.</param>
        public XRayTracer(IDDLogger logger)
        {
            _logger = logger;
            _xrayContext = new XRayTraceContext(logger);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XRayTracer"/> class with an API gateway proxy request.
        /// </summary>
        /// <param name="logger">DD logger.</param>
        /// <param name="request">API Gateway proxy requeset.</param>
        public XRayTracer(IDDLogger logger, APIGatewayProxyRequest request)
        {
            _logger = logger;
            _traceContext = PopulateDDContext(request.Headers);
            _xrayContext = new XRayTraceContext(logger);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XRayTracer"/> class with an IHeaderable.
        /// </summary>
        /// <param name="logger">DD logger.</param>
        /// <param name="headerable">An implementation of headerable.</param>
        public XRayTracer(IDDLogger logger, IHeaderable headerable)
        {
            _traceContext = PopulateDDContext(headerable.GetHeaders());
            _xrayContext = new XRayTraceContext(logger);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XRayTracer"/> class with a dummy _X_AMZN_TRACE_ID value for testing purposes.
        /// </summary>
        /// <param name="logger">DD logger.</param>
        /// <param name="xrayTraceInfo">XRay trace info.</param>
        internal XRayTracer(IDDLogger logger, string xrayTraceInfo)
        {
            _logger = logger;
            _xrayContext = new XRayTraceContext(xrayTraceInfo);
        }

        /// <summary>
        /// Gets the trace context.
        /// </summary>
        public DDTraceContext TraceContext
        {
            get
            {
                if (_traceContext == null)
                {
                    return new DDTraceContext();
                }

                return _traceContext;
            }
        }

        /// <summary>
        /// Gets the AWS XRay context.
        /// </summary>
        public XRayTraceContext XRayContext
        {
            get
            {
                if (_xrayContext == null)
                {
                    return new XRayTraceContext(_logger);
                }

                return _xrayContext;
            }
        }

        /// <summary>
        /// Gets the log correlation trace and span id map.
        /// </summary>
        /// <returns>dictionary with trace and span id.</returns>
        public Dictionary<string, string> GetLogCorrelationTraceAndSpanIDsMap()
        {
            if (_traceContext != null)
            {
                string traceID = _traceContext.TraceId;
                string spanID = _traceContext.ParentId;
                Dictionary<string, string> logMap = new Dictionary<string, string>();
                logMap.Add(TraceIdKey, traceID);
                logMap.Add(SpanIdKey, spanID);
                return logMap;
            }

            if (_xrayContext != null)
            {
                string traceID = _xrayContext.ApmTraceId;
                string spanID = _xrayContext.ApmParentId;
                Dictionary<string, string> logMap = new Dictionary<string, string>();
                logMap.Add(TraceIdKey, traceID);
                logMap.Add(SpanIdKey, spanID);
                return logMap;
            }

            _logger.Debug("No DD trace context or XRay trace context set!");
            return null;
        }

        /// <summary>
        /// Submit a segment to AWS XRay.
        /// </summary>
        /// <returns>true if sucessful, otherwise false.</returns>
        public Task<bool> SubmitSegmentAsync()
        {
            if (_traceContext == null)
            {
                _logger.Debug("Cannot submit a fake span on a null context. Is the DD tracing context being initialized correctly?");
                return Task.FromResult(false);
            }

            ConverterSubsegment es = new ConverterSubsegment(_logger, _traceContext, _xrayContext);
            return es.SendToXRayAsync();
        }

        /// <summary>
        /// Make the outbound HTTP trace headers.
        /// </summary>
        /// <returns>dictionary of headers.</returns>
        public Dictionary<string, string> MakeOutboundHttpTraceHeaders()
        {
            Dictionary<string, string> traceHeaders = new Dictionary<string, string>();

            string apmParent = null;
            if (_xrayContext != null)
            {
                apmParent = _xrayContext.ApmParentId;
            }

            if (_traceContext == null
                    || _traceContext == null
                    || _traceContext.TraceId == null
                    || _traceContext.SamplingPriority == null
                    || apmParent == null)
            {
                _logger.Debug("Cannot make outbound trace headers -- some required fields are null");
                return traceHeaders;
            }

            traceHeaders.Add(DDTraceContext.DdTraceKey, _traceContext.TraceId);
            traceHeaders.Add(DDTraceContext.DdSamplingKey, _traceContext.SamplingPriority);
            traceHeaders.Add(DDTraceContext.DdParentKey, _xrayContext.ApmParentId);

            return traceHeaders;
        }

        private string FormatLogCorrelation(string trace, string span)
        {
            return $"[dd.trace_id={trace} dd.span_id={span}";
        }

        /// <summary>
        /// Populate DD context with headers.
        /// </summary>
        /// <param name="headers">headers.</param>
        /// <returns>DD trace context.</returns>
        private DDTraceContext PopulateDDContext(IDictionary<string, string> headers)
        {
            DDTraceContext ctx = null;

            try
            {
                ctx = new DDTraceContext(_logger, headers);
            }
            catch
            {
                _logger.Debug("Unable to extract DD Trace Context from event headers");
            }

            return ctx;
        }
    }
}
