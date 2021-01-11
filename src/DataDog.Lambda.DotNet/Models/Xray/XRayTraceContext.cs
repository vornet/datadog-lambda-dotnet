using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace DataDog.Lambda.DotNet.Models.Xray
{
    public class XRayTraceContext
    {
        private IDDLogger _logger;

        public XRayTraceContext(IDDLogger logger)
        {
            _logger = logger;

            // Root=1-5e41a79d-e6a0db584029dba86a594b7e;Parent=8c34f5ad8f92d510;Sampled=1
            string traceId = Environment.GetEnvironmentVariable("_X_AMZN_TRACE_ID");
            if (string.IsNullOrEmpty(traceId))
            {
                _logger.Debug("Unable to find _X_AMZN_TRACE_ID");
                return;
            }

            string[] traceParts = traceId.Split(';');
            if (traceParts.Length != 3)
            {
                _logger.Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            try
            {
                TraceId = traceParts[0].Split('=')[1];
                ParentId = traceParts[1].Split('=')[1];
            }
            catch
            {
                _logger.Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            TraceIdHeader = traceId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XRayTraceContext"/> class with a dummy _X_AMZN_TRACE_ID value rather than reading from env vars.
        /// </summary>
        /// <param name="traceId">Trace id.</param>
        public XRayTraceContext(string traceId)
        {
            string[] traceParts = traceId.Split(';');
            if (traceParts.Length != 3)
            {
                _logger.Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            try
            {
                TraceId = traceParts[0].Split('=')[1];
                ParentId = traceParts[1].Split('=')[1];
            }
            catch
            {
                _logger.Error("Malformed _X_AMZN_TRACE_ID value: " + traceId);
                return;
            }

            TraceIdHeader = traceId;
        }

        [JsonPropertyName("traceIdHeader")]
        public string TraceIdHeader { get; set; }

        [JsonPropertyName("traceId")]
        public string TraceId { get; set; }

        [JsonPropertyName("parentId")]
        public string ParentId { get; set; }

        /// <summary>
        /// Gets the APM parent id.
        /// </summary>
        public string ApmParentId
            {
            get
                {
                try
                {
                    string lastSixteen = ParentId.Substring(this.ParentId.Length - 16);
                    ulong l_ApmId;
                    l_ApmId = ulong.Parse(lastSixteen, NumberStyles.HexNumber);
                    return l_ApmId.ToString();
                }
                catch (Exception e)
                {
                    _logger.Debug("Problem converting XRay Parent ID to APM Parent ID: " + e.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the APM trace id.
        /// </summary>
        public string ApmTraceId
            {
            get
                {
                // trace ID looks like 1-5e41a79d-e6a0db584029dba86a594b7e
                string bigId = string.Empty;
                try
                {
                    bigId = TraceId.Split('-')[2];
                }
                catch (Exception ex)
                {
                    if (ex is IndexOutOfRangeException || ex is NullReferenceException)
                    {
                        _logger.Debug("Unexpected format for the trace ID. Unable to parse it. " + TraceId);
                        return string.Empty;
                    }

                    throw;
                }

                // just to verify
                if (bigId.Length != 24)
                {
                    _logger.Debug("Got an unusual traceid from x-ray. Unable to convert that to an APM id. " + TraceId);
                    return string.Empty;
                }

                string last16 = bigId.Substring(bigId.Length - 16); // should be the last 16 characters of the big id

                ulong parsed;
                try
                {
                    parsed = ulong.Parse(last16, NumberStyles.HexNumber); // unsigned because parseLong throws a numberformatexception at anything greater than 0x7FFFF...
                }
                catch (Exception)
                {
                    _logger.Debug("Got a NumberFormatException trying to parse the traceID. Unable to convert to an APM id. " + TraceId);
                    return string.Empty;
                }

                parsed = parsed & 0x7FFFFFFFFFFFFFFFL; // take care of that pesky first bit...
                return parsed.ToString();
            }
        }

        /// <summary>
        /// Get tracing as a key/value pair.
        /// </summary>
        /// <returns>dictionary with tracing headers.</returns>
        public Dictionary<string, string> GetKeyValues()
        {
            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            if (TraceIdHeader != null)
            {
                keyValues.Add("X-Amzn-Trace-Id", TraceIdHeader);
            }

            return keyValues;
        }
    }
}
