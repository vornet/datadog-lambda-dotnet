using System.Collections.Generic;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Headerable is an interface that custom request objects must implement in order to benefit from Datadog tracing.
    /// <remarks>
    /// Headerable, when combined with the correct API Gateway Mapping Template 
    /// (including the default application/JSON and application/x-www-form-urlencoded templates) allow Lambda to write 
    /// the HTTP Request Headers to your custom Java event.
    /// </remarks>
    /// </summary>
    public interface IHeaderable
    {
        IDictionary<string, string> GetHeaders();
        void SetHeaders(IDictionary<string, string> headers);
    }
}