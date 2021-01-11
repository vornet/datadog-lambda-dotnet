using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Implementations should generate AWS X-Ray trace context for downstream requests.
    /// </summary>
    public interface IDDLambdaXrayTracer
    {
        /// <summary>
        /// Add trace headers to the HttpClient's default headers.
        /// </summary>
        /// <param name="httpClient">HTTP client.</param>
        void AddTraceHeaders(ref HttpClient httpClient);

        /// <summary>
        /// Add trace headers to the HttpRequestMessage headers.
        /// </summary>
        /// <param name="httpRequestMessage">HTTP requeset message.</param>
        void AddTraceHeaders(ref HttpRequestMessage httpRequestMessage);

        /// <summary>
        /// Get the trace context as a dictionary.
        /// </summary>
        /// <returns>dictionary of headers with trace context.</returns>
        Dictionary<string, string> GetTraceContext();

        /// <summary>
        /// Get the trace context as a string.
        /// </summary>
        /// <returns>trace context.</returns>
        string GetTraceContextString();
    }
}
