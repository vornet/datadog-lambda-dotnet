using System.Text.Json.Serialization;

namespace DataDog.Lambda.DotNet
{
    public class XraySubsegment
    {
        //{
        //  "start_time": 1500000000,
        //  "metadata": {
        //    "datadog": {
        //      "trace": {
        //        "trace-id": "abcdef",
        //        "sampling-priority": "1",
        //        "parent-id": "ghijk"
        //      }
        //    }
        //  },
        //  "trace_id": "1-5e41b3ba-9b515c884a780c0c63b74010",
        //  "parent_id": "30652c287aaff114",
        //  "name": "datadog-metadata",
        //  "end_time": 1500000001,
        //  "id": "30652c287aaff114",
        //  "type": "subsegment"
        //}

        private XraySubsegment()
        {
            //Initialize inner metadata structure
            MetadataDatadogTrace mdt = new MetadataDatadogTrace();
            MetadataDatadog md = new MetadataDatadog();
            MetadataCl m = new MetadataCl();

            md.Trace = mdt;
            m.Datadog = md;
            this.Metadata = m;
        }

        public class XraySubsegmentBuilder
        {
            private XraySubsegment _xrs;

            public XraySubsegmentBuilder()
            {
                _xrs = new XraySubsegment();
            }

            public XraySubsegmentBuilder StartTime(double startTime)
            {
                _xrs.StartTime = startTime;
                return this;
            }

            public XraySubsegmentBuilder EndTime(double endTime)
            {
                _xrs.EndTime = endTime;
                return this;
            }

            public XraySubsegmentBuilder TraceId(string traceId)
            {
                _xrs.TraceId = traceId;
                return this;
            }

            public XraySubsegmentBuilder ParentId(string parentId)
            {
                _xrs.ParentId = parentId;
                return this;
            }

            public XraySubsegmentBuilder Name(string name)
            {
                _xrs.Name = name;
                return this;
            }

            public XraySubsegmentBuilder Id(string id)
            {
                _xrs.Id = id;
                return this;
            }

            public XraySubsegmentBuilder Type(string type)
            {
                _xrs.Type = type;
                return this;
            }

            public XraySubsegmentBuilder DdTraceId(string traceId)
            {
                _xrs.Metadata.Datadog.Trace.TraceId = traceId;
                return this;
            }

            public XraySubsegmentBuilder DdSamplingPriority(string samplingPriority)
            {
                _xrs.Metadata.Datadog.Trace.SamplingPriority = samplingPriority;
                return this;
            }

            public XraySubsegmentBuilder DdParentId(string parentId)
            {
                _xrs.Metadata.Datadog.Trace.ParentId = parentId;
                return this;
            }


            public XraySubsegment Build()
            {
                return _xrs;
            }
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
        public string Name;

        [JsonPropertyName("end_time")]
        public double EndTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class MetadataDatadogTrace
    {
        [JsonPropertyName("trace-id")]
        public string TraceId { get; set; }

        [JsonPropertyName("sampling-priority")]
        public string SamplingPriority { get; set; }

        [JsonPropertyName("parent-id")]
        public string ParentId { get; set; }
    }

    public class MetadataDatadog
    {
        [JsonPropertyName("trace")]
        public MetadataDatadogTrace Trace { get; set; }
    }

    public class MetadataCl
    {
        [JsonPropertyName("datadog")]
        public MetadataDatadog Datadog { get; set; }
    }
}