using System.Text.Json.Serialization;

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace DataDog.Lambda.DotNet.Models.Xray
{
    internal class XraySubsegment
    {
        /*
        {
          "start_time": 1500000000,
          "metadata": {
            "datadog": {
              "trace": {
                "trace-id": "abcdef",
                "sampling-priority": "1",
                "parent-id": "ghijk"
              }
            }
          },
          "trace_id": "1-5e41b3ba-9b515c884a780c0c63b74010",
          "parent_id": "30652c287aaff114",
          "name": "datadog-metadata",
          "end_time": 1500000001,
          "id": "30652c287aaff114",
          "type": "subsegment"
        }
        */

        internal XraySubsegment()
        {
            // Initialize inner metadata structure
            MetadataDatadogTrace mdt = new MetadataDatadogTrace();
            MetadataDatadog md = new MetadataDatadog();
            MetadataCl m = new MetadataCl();

            md.Trace = mdt;
            m.Datadog = md;
            Metadata = m;
        }

        [JsonPropertyName("start_time")]

        public double StartTime { get; set; }

        [JsonPropertyName("metadata")]
        public MetadataCl Metadata { get; set; }

        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("end_time")]
        public double EndTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}