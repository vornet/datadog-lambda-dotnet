using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace DataDog.Lambda.DotNet.Models.Xray
{
    internal class ConverterSubsegment
    {
        private static readonly Random _random = new Random();
        private IDDLogger _logger;
        private DDTraceContext _traceContext;
        private XRayTraceContext _xrayContext;

        public ConverterSubsegment(IDDLogger logger, DDTraceContext traceContext, XRayTraceContext xrayContext)
        {
            _logger = logger;
            _traceContext = traceContext;
            _xrayContext = xrayContext;

            StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Name = "datadog-metadata";
            Type = "subsegment";

            byte[] idBytes = new byte[8];
            _random.NextBytes(idBytes);

            StringBuilder idStringBuilder = new StringBuilder();
            foreach (byte b in idBytes)
            {
                idStringBuilder.AppendFormat("{0:x2}", b);
            }

            Id = idStringBuilder.ToString();

            EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("start_time")]
        public double StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public double EndTime { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        public string ToJSONString()
        {
            XraySubsegmentBuilder xrb = new XraySubsegmentBuilder();
            XraySubsegment xrs = xrb.Name(Name)
                    .Id(Id)
                    .StartTime(StartTime)
                    .EndTime(EndTime)
                    .Type(Type)
                    .ParentId(_xrayContext.ParentId)
                    .TraceId(_xrayContext.TraceId)
                    .DdTraceId(_traceContext.TraceId)
                    .DdSamplingPriority(_traceContext.SamplingPriority)
                    .DdParentId(_traceContext.ParentId)
                    .Build();

            return JsonSerializer.Serialize(xrs);
        }

        public async Task<bool> SendToXRayAsync()
        {
            if (string.IsNullOrEmpty(Id))
            {
                return false;
            }

            string daemonIpString;
            string daemonPortString;
            string daemonAddress = Environment.GetEnvironmentVariable("AWS_XRAY_DAEMON_ADDRESS");
            if (daemonAddress != null)
            {
                if (daemonAddress.Split(':').Length != 2)
                {
                    _logger.Error("Unexpected AWS_XRAY_DAEMON_ADDRESS value: ", daemonAddress);
                    return false;
                }

                daemonIpString = daemonAddress.Split(':')[0];
                daemonPortString = daemonAddress.Split(':')[1];
                _logger.Debug("AWS XRay Address: ", daemonIpString);
                _logger.Debug("AWS XRay Port: ", daemonPortString);
            }
            else
            {
                _logger.Error("Unable to get AWS_XRAY_DAEMON_ADDRESS from environment vars");
                return false;
            }

            IPAddress daemonIpAddress;
            IPHostEntry hostEntry;

            hostEntry = Dns.GetHostEntry(daemonIpString);

            if (hostEntry.AddressList.Length > 0)
            {
                daemonIpAddress = hostEntry.AddressList[0];
            }
            else
            {
                _logger.Error("Unexpected exception looking up the AWS_XRAY_DAEMON_ADDRESS. This address should be a dotted quad and not require host resolution.");
                return false;
            }

            int daemonPort;
            try
            {
                daemonPort = int.Parse(daemonPortString);
            }
            catch (FormatException ex)
            {
                _logger.Error("Excepting parsing daemon port" + ex.Message);
                return false;
            }

            Dictionary<string, object> prefixMap = new Dictionary<string, object>();
            prefixMap.Add("format", "json");
            prefixMap.Add("version", 1);

            string message = this.ToJSONString();
            string payloadString = JsonSerializer.Serialize(prefixMap) + "\n" + message;

            byte[] payload = Encoding.UTF8.GetBytes(payloadString);

            UdpClient udpClient;
            try
            {
                udpClient = new UdpClient();
            }
            catch (SocketException e)
            {
                _logger.Error("Unable to bind to an available socket! " + e.Message);
                return false;
            }

            try
            {
                await udpClient.SendAsync(payload, payload.Length, daemonIpAddress.ToString(), daemonPort);
            }
            catch (IOException e)
            {
                _logger.Error("Couldn't send packet! " + e.Message);
                return false;
            }

            return true;
        }
    }
}
