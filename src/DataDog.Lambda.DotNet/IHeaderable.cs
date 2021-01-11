using System.Collections.Generic;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Implementation should provider headers.
    /// <remarks>
    /// Headerable, when combined with the correct API Gateway Mapping Template
    /// (including the default application/JSON and application/x-www-form-urlencoded templates)
    /// allow Lambda to write the HTTP Request Headers to your custom metric.
    /// </remarks>
    /// </summary>
    public interface IHeaderable
    {
        /// <summary>
        /// Get headers.
        /// </summary>
        /// <returns>headers.</returns>
        IDictionary<string, string> GetHeaders();

        /// <summary>
        /// Set headers.
        /// </summary>
        /// <param name="headers">headers.</param>
        void SetHeaders(IDictionary<string, string> headers);
    }
}