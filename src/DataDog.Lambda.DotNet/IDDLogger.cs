namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Logging level.
    /// </summary>
    public enum LoggingLevel
    {
        /// <summary>
        /// Debug or lower.
        /// </summary>
        Debug,

        /// <summary>
        /// Error or lower.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Implementations should log to DataDog.
    /// </summary>
    public interface IDDLogger
    {
        /// <summary>
        /// Log a debug level message.
        /// </summary>
        /// <param name="message">message to log.</param>
        /// <param name="tags">tags to include.</param>
        void Debug(string message, params string[] tags);

        /// <summary>
        /// Log a error level message.
        /// </summary>
        /// <param name="message">message to log.</param>
        /// <param name="tags">tags to include.</param>
        void Error(string message, params string[] tags);

        /// <summary>
        /// Set the log level.
        /// </summary>
        /// <param name="level">Log level.</param>
        void SetLevel(LoggingLevel level);
    }
}