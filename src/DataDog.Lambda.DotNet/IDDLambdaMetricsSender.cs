using System;
using System.Collections.Generic;
using System.Net.Http;
using Amazon.Lambda.Core;

namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Implementations of this should report AWS Lambda metrics to DataDog.
    /// </summary>
    public interface IDDLambdaMetricsSender
    {
        /// <summary>
        /// Increments the aws.lambda.enhanced.error metric in Datadog.
        /// </summary>
        void IncrementError();

        /// <summary>
        /// Report enchanced custom metrics to DataDog.
        /// </summary>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value of the metric.</param>
        /// <param name="tags">Tags for the metric.</param>
        void SendCustom(string name, double value, Dictionary<string, string> tags);

        /// <summary>
        /// Report enchanced custom metrics to DataDog with a date.
        /// </summary>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value of the metric.</param>
        /// <param name="tags">Tags for the metric.</param>
        /// <param name="date">When the event happened.</param>
        void SendCustom(string name, double value, Dictionary<string, string> tags, DateTimeOffset date);
    }
}