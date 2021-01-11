#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace DataDog.Lambda.DotNet.Models.Xray
{
    internal class XraySubsegmentBuilder
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
}