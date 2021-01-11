using System.Text.Json.Serialization;

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace DataDog.Lambda.DotNet.Models.Xray
{
    internal class MetadataCl
    {
        [JsonPropertyName("datadog")]
        public MetadataDatadog Datadog { get; set; }
    }
}