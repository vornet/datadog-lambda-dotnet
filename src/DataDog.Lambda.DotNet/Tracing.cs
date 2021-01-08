using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DataDog.Lambda.DotNet;
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

namespace DataDog.Lambda.DotNet
{
    public class Tracing
    {
        public const string TraceIdKey = "dd.trace_id";
        public const string SpanIdKey = "dd.span_id";

        private ILambdaLogger _logger;
        protected DDTraceContext _traceContext;
        protected XRayTraceContext _xrayContext;

        public Tracing(ILambdaLogger logger)
        {
            _logger = logger;
            _xrayContext = new XRayTraceContext(logger);
        }

        public Tracing(ILambdaLogger logger, APIGatewayProxyRequest req)
        {
            _logger = logger;
            _traceContext = PopulateDDContext(logger, req.Headers);
            _xrayContext = new XRayTraceContext(logger);
        }

        public Tracing(ILambdaLogger logger, IHeaderable req)
        {
            _traceContext = PopulateDDContext(logger, req.GetHeaders());
            _xrayContext = new XRayTraceContext(logger);
        }

        /// <summary>
        /// Test constructor that can take a dummy _X_AMZN_TRACE_ID value
        /// </summary>
        /// <param name="xrayTraceInfo"></param>
        public Tracing(ILambdaLogger logger, string xrayTraceInfo)
        {
            _logger = logger;
            _xrayContext = new XRayTraceContext(xrayTraceInfo);
        }

        public DDTraceContext TraceContext {
            get {
                if (_traceContext == null)
                {
                    return new DDTraceContext();
                }
                return _traceContext;
            }
        }

        public XRayTraceContext XrayContext {
            get {
                if (_xrayContext == null)
                {
                    return new XRayTraceContext(_logger);
                }
                return _xrayContext;
            }
        }

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
            DDLogger.GetLoggerImpl(_logger).Debug("No DD trace context or XRay trace context set!");
            return null;
        }

        private string FormatLogCorrelation(String trace, String span)
        {
            return $"[dd.trace_id={trace} dd.span_id={span}";
        }

        private static DDTraceContext PopulateDDContext(ILambdaLogger logger, IDictionary<string, string> headers)
        {
            DDTraceContext ctx = null;
            try
            {
                ctx = new DDTraceContext(logger, headers);
            }
            catch
            {
                DDLogger.GetLoggerImpl(logger).Debug("Unable to extract DD Trace Context from event headers");
            }
            return ctx;
        }

        public Task<bool> SubmitSegment(ILambdaLogger logger)
        {
            if (_traceContext == null)
            {
                DDLogger.GetLoggerImpl(logger).Debug("Cannot submit a fake span on a null context. Is the DD tracing context being initialized correctly?");
                return Task.FromResult(false);
            }

            ConverterSubsegment es = new ConverterSubsegment(_traceContext, _xrayContext);
            return es.SendToXRayAsync(logger);
        }

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
                DDLogger.GetLoggerImpl(_logger).Debug("Cannot make outbound trace headers -- some required fields are null");
                return traceHeaders;
            }

            traceHeaders.Add(DDTraceContext.DdTraceKey, _traceContext.TraceId);
            traceHeaders.Add(DDTraceContext.DdSamplingKey, _traceContext.SamplingPriority);
            traceHeaders.Add(DDTraceContext.DdParentKey, _xrayContext.ApmParentId);

            return traceHeaders;
        }
    }

    public class ConverterSubsegment
    {

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("start_time")]
        public double StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public double EndTime { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        private static Random random = new Random();
        private DDTraceContext _traceContext;
        private XRayTraceContext _xrayContext;

        public ConverterSubsegment(DDTraceContext traceContext, XRayTraceContext xrayContext)
        {
            _traceContext = traceContext;
            _xrayContext = xrayContext;

            StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Name = "datadog-metadata";
            Type = "subsegment";

            byte[] idBytes = new byte[8];
            random.NextBytes(idBytes);

            StringBuilder idStringBuilder = new StringBuilder();
            foreach (byte b in idBytes)
            {
                idStringBuilder.AppendFormat("{0:x2}", b);
            }

            Id = idStringBuilder.ToString();

            EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public string ToJSONString()
        {

            XraySubsegment.XraySubsegmentBuilder xrb = new XraySubsegment.XraySubsegmentBuilder();
            XraySubsegment xrs = xrb.Name(Name)
                    .Id(Id)
                    .StartTime(StartTime)
                    .EndTime(EndTime)
                    .Type(Type)
                    .ParentId(_xrayContext.ParentId)
                    .TraceId(_xrayContext.TraceId)
                    .DdTraceId(_traceContext.TraceId)
                    .DdSamplingPriority(_traceContext.SamplingPriority)
                    .DdParentId(_traceContext.ParentId)
                    .Build();

            return JsonSerializer.Serialize(xrs);
        }

        public async Task<bool> SendToXRayAsync(ILambdaLogger logger)
        {
            if (string.IsNullOrEmpty(Id))
            {
                return false;
            }

            string daemonIpString;
            string daemonPortString;
            string daemonAddress = Environment.GetEnvironmentVariable("AWS_XRAY_DAEMON_ADDRESS");
            if (daemonAddress != null)
            {
                if (daemonAddress.Split(':').Length != 2)
                {
                    DDLogger.GetLoggerImpl(logger).Error("Unexpected AWS_XRAY_DAEMON_ADDRESS value: ", daemonAddress);
                    return false;
                }
                daemonIpString = daemonAddress.Split(':')[0];
                daemonPortString = daemonAddress.Split(':')[1];
                DDLogger.GetLoggerImpl(logger).Debug("AWS XRay Address: ", daemonIpString);
                DDLogger.GetLoggerImpl(logger).Debug("AWS XRay Port: ", daemonPortString);
            }
            else
            {
                DDLogger.GetLoggerImpl(logger).Error("Unable to get AWS_XRAY_DAEMON_ADDRESS from environment vars");
                return false;
            }

            IPAddress daemonIpAddress;
            IPHostEntry hostEntry;

            hostEntry = Dns.GetHostEntry(daemonIpString);

            if (hostEntry.AddressList.Length > 0)
            {
                daemonIpAddress = hostEntry.AddressList[0];
            }
            else
            {
                DDLogger.GetLoggerImpl(logger).Error("Unexpected exception looking up the AWS_XRAY_DAEMON_ADDRESS. This address should be a dotted quad and not require host resolution.");
                return false;
            }

            int daemonPort;
            try
            {
                daemonPort = int.Parse(daemonPortString);
            }
            catch (FormatException ex)
            {
                DDLogger.GetLoggerImpl(logger).Error("Excepting parsing daemon port" + ex.Message);
                return false;
            }

            Dictionary<string, object> prefixMap = new Dictionary<string, object>();
            prefixMap.Add("format", "json");
            prefixMap.Add("version", 1);

            string message = this.ToJSONString();
            string payloadString = JsonSerializer.Serialize(prefixMap) + "\n" + message;

            byte[] payload = Encoding.UTF8.GetBytes(payloadString);

            UdpClient udpClient;
            try
            {
                udpClient = new UdpClient();
            }
            catch (SocketException e)
            {
                DDLogger.GetLoggerImpl(logger).Error("Unable to bind to an available socket! " + e.Message);
                return false;
            }
            try
            {
                await udpClient.SendAsync(payload, payload.Length, daemonIpAddress.ToString(), daemonPort);
            }
            catch (IOException e)
            {
                DDLogger.GetLoggerImpl(logger).Error("Couldn't send packet! " + e.Message);
                return false;
            }
            return true;
        }

    }

    public class DDTraceContext
    {
        public const string DdTraceKey = "x-datadog-trace-id";
        public const string DdParentKey = "x-datadog-parent-id";
        public const string DdSamplingKey = "x-datadog-sampling-priority";

        public string TraceId { get; set; }

        public string ParentId { get; set; }

        public string SamplingPriority { get; set; }

        public DDTraceContext()
        {
        }

        public DDTraceContext(ILambdaLogger logger, IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                DDLogger.GetLoggerImpl(logger).Debug("Unable to extract DD Context from null headers");
                throw new Exception("null headers!");
            }
            headers = ToLowerKeys(headers);

            if (!headers.ContainsKey(DdTraceKey))
            {
                DDLogger.GetLoggerImpl(logger).Debug("Headers missing the DD Trace ID");
                throw new Exception("No trace ID");
            }
            TraceId = headers[DdTraceKey];

            if (!headers.ContainsKey(DdParentKey))
            {
                DDLogger.GetLoggerImpl(logger).Debug("Headers missing the DD Parent ID");
                throw new Exception("Missing Parent ID");
            }
            ParentId = headers[DdParentKey];

            if (!headers.ContainsKey(DdSamplingKey))
            {
                DDLogger.GetLoggerImpl(logger).Debug("Headers missing the DD Sampling Priority. Defaulting to '2'");
                headers.Add(DdSamplingKey, "2");
            }
            SamplingPriority = headers[DdSamplingKey];
        }

        private Dictionary<string, string> ToLowerKeys(IDictionary<string, string> headers)
        {
            Dictionary<string, string> headers2 = new Dictionary<string, string>();

            foreach (var entry in headers)
            {
                string k = entry.Key;
                string v = entry.Value;
                headers2.Add(k, v);
                headers2.Add(k.ToLower(), v);
            }

            return headers2;
        }

        public Dictionary<string, string> ToJsonMap()
        {
            Dictionary<string, string> jo = new Dictionary<string, string>();
            jo.Add("trace-id", TraceId);
            jo.Add("parent-id", ParentId);
            jo.Add("sampling-priority", SamplingPriority);
            return jo;
        }

        public Dictionary<string, string> getKeyValues()
        {
            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            if (TraceId != null)
            {
                keyValues.Add(DdTraceKey, TraceId);
            }
            if (ParentId != null)
            {
                keyValues.Add(DdParentKey, ParentId);
            }

            if (SamplingPriority != null)
            {
                keyValues.Add(DdSamplingKey, SamplingPriority);
            }
            return keyValues;
        }
    }

    public class XRayTraceContext
    {
        [JsonPropertyName("traceIdHeader")]
        public string TraceIdHeader { get; set; }

        [JsonPropertyName("traceId")]
        public string TraceId { get; set; }

        [JsonPropertyName("parentId")]
        public string ParentId { get; set; }

        private ILambdaLogger _logger;

        public XRayTraceContext(ILambdaLogger logger)
        {
            _logger = logger;

            //Root=1-5e41a79d-e6a0db584029dba86a594b7e;Parent=8c34f5ad8f92d510;Sampled=1
            string traceId = Environment.GetEnvironmentVariable("_X_AMZN_TRACE_ID");
            if (string.IsNullOrEmpty(traceId))
            {
                DDLogger.GetLoggerImpl(logger).Debug("Unable to find _X_AMZN_TRACE_ID");
                return;
            }

            string[] traceParts = traceId.Split(';');
            if (traceParts.Length != 3)
            {
                DDLogger.GetLoggerImpl(logger).Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            try
            {
                TraceId = traceParts[0].Split('=')[1];
                ParentId = traceParts[1].Split('=')[1];
            }
            catch
            {
                DDLogger.GetLoggerImpl(logger).Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }
            TraceIdHeader = traceId;
        }

        /**
         * Test constructor that can take a dummy _X_AMZN_TRACE_ID value rather than reading from env vars
         * @param traceId
         */
        public XRayTraceContext(string traceId)
        {
            string[] traceParts = traceId.Split(';');
            if (traceParts.Length != 3)
            {
                DDLogger.GetLoggerImpl(_logger).Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            try
            {
                TraceId = traceParts[0].Split('=')[1];
                ParentId = traceParts[1].Split('=')[1];
            }
            catch
            {
                DDLogger.GetLoggerImpl(_logger).Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }
            TraceIdHeader = traceId;
        }

        public Dictionary<string, string> GetKeyValues()
        {
            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            if (TraceIdHeader != null)
            {
                keyValues.Add("X-Amzn-Trace-Id", TraceIdHeader);
            }
            return keyValues;
        }

        public string ApmParentId {
            get {
                try
                {
                    string lastSixteen = ParentId.Substring(this.ParentId.Length - 16);
                    ulong l_ApmId;
                    l_ApmId = ulong.Parse(lastSixteen, NumberStyles.HexNumber);
                    return l_ApmId.ToString();
                }
                catch (Exception e)
                {
                    DDLogger.GetLoggerImpl(_logger).Debug("Problem converting XRay Parent ID to APM Parent ID: " + e.Message);
                    return null;
                }
            }
        }

        public string ApmTraceId {
            get {
                //trace ID looks like 1-5e41a79d-e6a0db584029dba86a594b7e
                string bigId = "";
                try
                {
                    bigId = TraceId.Split('-')[2];
                }
                catch (Exception ex)
                {
                    if (ex is IndexOutOfRangeException || ex is NullReferenceException)
                    {
                        DDLogger.GetLoggerImpl(_logger).Debug("Unexpected format for the trace ID. Unable to parse it. " + TraceId);
                        return string.Empty;
                    }

                    throw;
                }

                //just to verify
                if (bigId.Length != 24)
                {
                    DDLogger.GetLoggerImpl(_logger).Debug("Got an unusual traceid from x-ray. Unable to convert that to an APM id. " + TraceId);
                    return "";
                }

                string last16 = bigId.Substring(bigId.Length - 16); // should be the last 16 characters of the big id

                ulong parsed;
                try
                {
                    parsed = ulong.Parse(last16, NumberStyles.HexNumber); //unsigned because parseLong throws a numberformatexception at anything greater than 0x7FFFF...
                }
                catch (Exception)
                {
                    DDLogger.GetLoggerImpl(_logger).Debug("Got a NumberFormatException trying to parse the traceID. Unable to convert to an APM id. " + TraceId);
                    return "";
                }
                parsed = parsed & 0x7FFFFFFFFFFFFFFFL; //take care of that pesky first bit...
                return parsed.ToString();
            }
        }
    }
}
