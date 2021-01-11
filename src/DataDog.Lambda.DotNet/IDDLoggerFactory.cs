namespace DataDog.Lambda.DotNet
{
    /// <summary>
    /// Implementation should return a DD logger.
    /// </summary>
    public interface IDDLoggerFactory
    {
        /// <summary>
        /// Create a DD logger.
        /// </summary>
        /// <returns>DD logger.</returns>
        IDDLogger GetLogger();
    }
}
