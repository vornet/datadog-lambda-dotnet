using System.Text.Json.Serialization;

#pragma warning disable SA1600 // Elements should be documented

namespace DataDog.Lambda.DotNet.Models.Xray
{
    internal class MetadataDatadog
    {
        [JsonPropertyName("trace")]
        public MetadataDatadogTrace Trace { get; set; }
    }
}