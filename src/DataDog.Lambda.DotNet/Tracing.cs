using Amazon.Lambda.APIGatewayEvents;
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

        protected DDTraceContext _context;
        protected XRayTraceContext _xrayContext;

        public Tracing()
        {
            _xrayContext = new XRayTraceContext();
        }

        public Tracing(APIGatewayProxyRequest req)
        {
            _context = PopulateDDContext(req.Headers);
            _xrayContext = new XRayTraceContext();
        }

        public Tracing(IHeaderable req)
        {
            _context = PopulateDDContext(req.GetHeaders());
            _xrayContext = new XRayTraceContext();
        }

        /// <summary>
        /// Test constructor that can take a dummy _X_AMZN_TRACE_ID value
        /// </summary>
        /// <param name="xrayTraceInfo"></param>
        public Tracing(string xrayTraceInfo)
        {
            _xrayContext = new XRayTraceContext(xrayTraceInfo);
        }

        public DDTraceContext DDContext {
            get {
                if (_context == null)
                {
                    return new DDTraceContext();
                }
                return _context;
            }
        }

        public XRayTraceContext XrayContext {
            get {
                if (_xrayContext == null)
                {
                    return new XRayTraceContext();
                }
                return _xrayContext;
            }
        }

        public Dictionary<string, string> GetLogCorrelationTraceAndSpanIDsMap()
        {
            if (_context != null)
            {
                string traceID = _context.TraceId;
                string spanID = _context.ParentId;
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
            DDLogger.GetLoggerImpl().Debug("No DD trace context or XRay trace context set!");
            return null;
        }

        private string FormatLogCorrelation(String trace, String span)
        {
            return $"[dd.trace_id={trace} dd.span_id={span}";
        }

        private static DDTraceContext PopulateDDContext(IDictionary<string, string> headers)
        {
            DDTraceContext ctx = null;
            try
            {
                ctx = new DDTraceContext(headers);
            }
            catch (Exception e)
            {
                DDLogger.GetLoggerImpl().Debug("Unable to extract DD Trace Context from event headers");
            }
            return ctx;
        }

        public Task<bool> SubmitSegment()
        {
            if (_context == null)
            {
                DDLogger.GetLoggerImpl().Debug("Cannot submit a fake span on a null context. Is the DD tracing context being initialized correctly?");
                return Task.FromResult(false);
            }

            ConverterSubsegment es = new ConverterSubsegment(_context, _xrayContext);
            return es.SendToXRayAsync();
        }

        public Dictionary<string, string> MakeOutboundHttpTraceHeaders()
        {
            Dictionary<string, string> traceHeaders = new Dictionary<string, string>();

            string apmParent = null;
            if (_xrayContext != null)
            {
                apmParent = _xrayContext.ApmParentId;
            }
            if (_context == null
                    || _context == null
                    || _context.TraceId == null
                    || _context.SamplingPriority == null
                    || apmParent == null)
            {
                DDLogger.GetLoggerImpl().Debug("Cannot make outbound trace headers -- some required fields are null");
                return traceHeaders;
            }

            traceHeaders.Add(DDTraceContext.DdTraceKey, _context.TraceId);
            traceHeaders.Add(DDTraceContext.DdSamplingKey, _context.SamplingPriority);
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
        private DDTraceContext _context;
        private XRayTraceContext _xrayContext;

        public ConverterSubsegment(DDTraceContext ctx, XRayTraceContext xrt)
        {
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
                    .DdTraceId(_context.TraceId)
                    .DdSamplingPriority(_context.SamplingPriority)
                    .DdParentId(_context.ParentId)
                    .Build();

            return JsonSerializer.Serialize(xrs);
        }

        public async Task<bool> SendToXRayAsync()
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
                    DDLogger.GetLoggerImpl().Error("Unexpected AWS_XRAY_DAEMON_ADDRESS value: ", daemonAddress);
                    return false;
                }
                daemonIpString = daemonAddress.Split(':')[0];
                daemonPortString = daemonAddress.Split(':')[1];
                DDLogger.GetLoggerImpl().Debug("AWS XRay Address: ", daemonIpString);
                DDLogger.GetLoggerImpl().Debug("AWS XRay Port: ", daemonPortString);
            }
            else
            {
                DDLogger.GetLoggerImpl().Error("Unable to get AWS_XRAY_DAEMON_ADDRESS from environment vars");
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
                DDLogger.GetLoggerImpl().Error("Unexpected exception looking up the AWS_XRAY_DAEMON_ADDRESS. This address should be a dotted quad and not require host resolution.");
                return false;
            }

            int daemonPort;
            try
            {
                daemonPort = int.Parse(daemonPortString);
            }
            catch (FormatException ex)
            {
                DDLogger.GetLoggerImpl().Error("Excepting parsing daemon port" + ex.Message);
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
                DDLogger.GetLoggerImpl().Error("Unable to bind to an available socket! " + e.Message);
                return false;
            }
            try
            {
                await udpClient.SendAsync(payload, payload.Length, daemonIpAddress.ToString(), daemonPort);
            }
            catch (IOException e)
            {
                DDLogger.GetLoggerImpl().Error("Couldn't send packet! " + e.Message);
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

        public DDTraceContext(IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                DDLogger.GetLoggerImpl().Debug("Unable to extract DD Context from null headers");
                throw new Exception("null headers!");
            }
            headers = ToLowerKeys(headers);

            if (!headers.ContainsKey(DdTraceKey))
            {
                DDLogger.GetLoggerImpl().Debug("Headers missing the DD Trace ID");
                throw new Exception("No trace ID");
            }
            TraceId = headers[DdTraceKey];

            if (!headers.ContainsKey(DdParentKey))
            {
                DDLogger.GetLoggerImpl().Debug("Headers missing the DD Parent ID");
                throw new Exception("Missing Parent ID");
            }
            ParentId = headers[DdParentKey];

            if (!headers.ContainsKey(DdSamplingKey))
            {
                DDLogger.GetLoggerImpl().Debug("Headers missing the DD Sampling Priority. Defaulting to '2'");
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

        public XRayTraceContext()
        {
            //Root=1-5e41a79d-e6a0db584029dba86a594b7e;Parent=8c34f5ad8f92d510;Sampled=1
            string traceId = Environment.GetEnvironmentVariable("_X_AMZN_TRACE_ID");
            if (string.IsNullOrEmpty(traceId))
            {
                DDLogger.GetLoggerImpl().Debug("Unable to find _X_AMZN_TRACE_ID");
                return;
            }

            string[] traceParts = traceId.Split(';');
            if (traceParts.Length != 3)
            {
                DDLogger.GetLoggerImpl().Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            try
            {
                TraceId = traceParts[0].Split('=')[1];
                ParentId = traceParts[1].Split('=')[1];
            }
            catch
            {
                DDLogger.GetLoggerImpl().Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
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
                DDLogger.GetLoggerImpl().Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            try
            {
                TraceId = traceParts[0].Split('=')[1];
                ParentId = traceParts[1].Split('=')[1];
            }
            catch (Exception e)
            {
                DDLogger.GetLoggerImpl().Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
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
                    DDLogger.GetLoggerImpl().Debug("Problem converting XRay Parent ID to APM Parent ID: " + e.Message);
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
                        DDLogger.GetLoggerImpl().Debug("Unexpected format for the trace ID. Unable to parse it. " + TraceId);
                        return string.Empty;
                    }

                    throw;
                }

                //just to verify
                if (bigId.Length != 24)
                {
                    DDLogger.GetLoggerImpl().Debug("Got an unusual traceid from x-ray. Unable to convert that to an APM id. " + TraceId);
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
                    DDLogger.GetLoggerImpl().Debug("Got a NumberFormatException trying to parse the traceID. Unable to convert to an APM id. " + TraceId);
                    return "";
                }
                parsed = parsed & 0x7FFFFFFFFFFFFFFFL; //take care of that pesky first bit...
                return parsed.ToString();
            }
        }
    }
}
