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

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Datadog trace context.
    /// </summary>
    public class DDTraceContext
    {
        /// <summary>
        /// DataDog trace key.
        /// </summary>
        internal const string DdTraceKey = "x-datadog-trace-id";

        /// <summary>
        /// DataDog parent key.
        /// </summary>
        internal const string DdParentKey = "x-datadog-parent-id";

        /// <summary>
        /// DataDog sampling key.
        /// </summary>
        internal const string DdSamplingKey = "x-datadog-sampling-priority";

        /// <summary>
        /// Initializes a new instance of the <see cref="DDTraceContext"/> class.
        /// </summary>
        public DDTraceContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DDTraceContext"/> class.
        /// </summary>
        /// <param name="logger">DD logger.</param>
        /// <param name="headers">dictionary of headers.</param>
        public DDTraceContext(IDDLogger logger, IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                logger.Debug("Unable to extract DD Context from null headers");
                throw new Exception("null headers!");
            }

            headers = ToLowerKeys(headers);

            if (!headers.ContainsKey(DdTraceKey))
            {
                logger.Debug("Headers missing the DD Trace ID");
                throw new Exception("No trace ID");
            }

            TraceId = headers[DdTraceKey];

            if (!headers.ContainsKey(DdParentKey))
            {
                logger.Debug("Headers missing the DD Parent ID");
                throw new Exception("Missing Parent ID");
            }

            ParentId = headers[DdParentKey];

            if (!headers.ContainsKey(DdSamplingKey))
            {
                logger.Debug("Headers missing the DD Sampling Priority. Defaulting to '2'");
                headers.Add(DdSamplingKey, "2");
            }

            SamplingPriority = headers[DdSamplingKey];
        }

        /// <summary>
        /// Gets or sets trace id.
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Gets or sets parent id.
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// Gets or sets sampling priority.
        /// </summary>
        public string SamplingPriority { get; set; }

        /// <summary>
        /// Convert to a JSON map.
        /// </summary>
        /// <returns>JSON map.</returns>
        public Dictionary<string, string> ToJsonMap()
        {
            Dictionary<string, string> jo = new Dictionary<string, string>();
            jo.Add("trace-id", TraceId);
            jo.Add("parent-id", ParentId);
            jo.Add("sampling-priority", SamplingPriority);
            return jo;
        }

        /// <summary>
        /// Get key/value pairs.
        /// </summary>
        /// <returns>Dictionary of key/value pairs.</returns>
        public Dictionary<string, string> GetKeyValues()
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
    }
}
