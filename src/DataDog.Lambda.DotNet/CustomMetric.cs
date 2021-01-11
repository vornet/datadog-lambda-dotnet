using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// A custom AWS Lambda DataDog metric.
    /// </summary>
    public class CustomMetric
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMetric"/> class.
        /// </summary>
        /// <param name="metric">Metric name.</param>
        /// <param name="value">Metric value.</param>
        /// <param name="tags">Metric tags.</param>
        /// <param name="eventTime">The event time.</param>
        public CustomMetric(string metric, double value, IDictionary<string, string> tags, DateTimeOffset eventTime)
        {
            Metric = metric;
            Value = value;

            // First we need to turn the tags into an array of colon-delimited strings
            IEnumerable<string> tagsList = null;

            if (tags != null)
            {
                tagsList = tags.Select((kvp) => $"{kvp.Key}:{kvp.Value}");
            }

            Tags = tagsList;
            EventTime = eventTime.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Gets the metric name.
        /// </summary>
        [JsonPropertyName("m")]
        public string Metric { get; }

        /// <summary>
        /// Gets the metric value.
        /// </summary>
        [JsonPropertyName("v")]
        public double Value { get; }

        /// <summary>
        /// Gets the tags.
        /// </summary>
        [JsonPropertyName("t")]
        public IEnumerable<string> Tags { get; }

        /// <summary>
        /// Gets the event date/time in epoch (Unix timestamp).
        /// </summary>
        [JsonPropertyName("e")]
        public long EventTime { get; }

        /// <summary>
        /// Generate JSON string.
        /// </summary>
        /// <returns>JSON string.</returns>
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}