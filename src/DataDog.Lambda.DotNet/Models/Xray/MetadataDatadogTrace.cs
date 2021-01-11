using System.Text.Json.Serialization;

#pragma warning disable SA1600 // Elements should be documented

namespace DataDog.Lambda.DotNet.Models.Xray
{
    internal class MetadataDatadogTrace
    {
        [JsonPropertyName("trace-id")]

        public string TraceId { get; set; }

        [JsonPropertyName("sampling-priority")]
        public string SamplingPriority { get; set; }

        [JsonPropertyName("parent-id")]
        public string ParentId { get; set; }
    }
}