using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// All the information for a custom Datadog distribution metric.
    /// </summary>
    public class CustomMetric
    {
        private string _name;
        private double _value;
        private IDictionary<string, object> _tags;
        private DateTimeOffset _time;

        /// <summary>
        /// Create a custom distribution metric
        /// </summary>
        /// <param name="name">The name assigned to the metric</param>
        /// <param name="value">The value of the metric</param>
        /// <param name="tags">A map of tags(if any) that you want to assign to the metric</param>
        public CustomMetric(string name, double value, IDictionary<string, object> tags) : this(name, value, tags, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Create a custom distribution metric with custom a custom time
        /// </summary>
        /// <param name="name">The name assigned to the metric</param>
        /// <param name="value">The value of the metric</param>
        /// <param name="tags">A map of tags(if any) that you want to assign to the metric</param>
        /// <param name="time">The time that you want to give to the metric</param>
        public CustomMetric(string name, double value, IDictionary<string, object> tags, DateTimeOffset time)
        {
            _name = name;
            _value = value;
            _tags = tags;
            _time = time;
        }

        /// <summary>
        /// Create a JSON string representing the distribution metric
        /// </summary>
        /// <returns>the Metric's JSON representation</returns>
        public string ToJson()
        {
            PersistedCustomMetric pcm = new PersistedCustomMetric(_name, _value, _tags, _time);
            return pcm.ToJsonString();
        }

        /// <summary>
        /// Write writes the CustomMetric to Datadog
        /// </summary>
        public void Write()
        {
            MetricWriter writer = MetricWriter.GetMetricWriterImpl();
            writer.Write(this);
        }
    }

    public class PersistedCustomMetric
    {
        public PersistedCustomMetric(string m, double v, IDictionary<string, object> t, DateTimeOffset e)
        {
            Metric = m;
            Value = v;

            // First we need to turn the tags into an array of colon-delimited strings
            IEnumerable<string> tagsList = null;

            if (t != null)
            {
                tagsList = t.Select((kvp) => $"{kvp.Key}:{kvp.Value}");
            }

            Tags = tagsList;
            EventTime = e.ToUnixTimeSeconds();
        }

        [JsonPropertyName("m")]
        public string Metric { get; }

        [JsonPropertyName("v")]
        public double Value { get; }

        [JsonPropertyName("t")]
        public IEnumerable<string> Tags { get; }

        [JsonPropertyName("e")]
        public long EventTime { get; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}