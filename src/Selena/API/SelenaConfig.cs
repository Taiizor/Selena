namespace Selena.API
{
    /// <summary>
    /// Configuration options for Selena channel.
    /// </summary>
    public class SelenaConfig
    {
        /// <summary>
        /// Size of the memory-mapped file buffer in bytes. Default: 1MB.
        /// </summary>
        public int BufferSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Unique name for the channel.
        /// </summary>
        public string ChannelName { get; set; } = "DefaultChannel";

        /// <summary>
        /// Strategy to use when buffer is full.
        /// </summary>
        public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.Overwrite;

        /// <summary>
        /// Polling interval in milliseconds for Linux/macOS. Default: 10ms.
        /// </summary>
        public int PollingInterval { get; set; } = 10;

        /// <summary>
        /// Enable JSON logging for debugging purposes.
        /// </summary>
        public bool EnableJsonLogging { get; set; } = false;

        /// <summary>
        /// Maximum wait time for blocking operations in milliseconds. Default: 5000ms.
        /// </summary>
        public int MaxWaitTime { get; set; } = 5000;

        /// <summary>
        /// Scope mode for Windows IPC objects. Default: Local.
        /// </summary>
        public ScopeMode ScopeMode { get; set; } = ScopeMode.Local;

        /// <summary>
        /// Enable optimized mode with memory pooling and compression. Default: true.
        /// </summary>
        public bool EnableOptimizations { get; set; } = true;

        /// <summary>
        /// Enable compression for messages larger than threshold. Default: true.
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Minimum message size in bytes to apply compression. Default: 1024 (1KB).
        /// </summary>
        public int CompressionThreshold { get; set; } = 1024;

        /// <summary>
        /// Use reader/writer locks for better concurrency. Default: true.
        /// </summary>
        public bool UseReaderWriterLocks { get; set; } = true;

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (BufferSize < 1024)
            {
                throw new ArgumentException("BufferSize must be at least 1KB");
            }

            if (string.IsNullOrWhiteSpace(ChannelName))
            {
                throw new ArgumentException("ChannelName cannot be empty");
            }

            if (PollingInterval is < 1 or > 1000)
            {
                throw new ArgumentException("PollingInterval must be between 1 and 1000 ms");
            }

            if (MaxWaitTime < 0)
            {
                throw new ArgumentException("MaxWaitTime must be non-negative");
            }

            if (CompressionThreshold < 256)
            {
                throw new ArgumentException("CompressionThreshold must be at least 256 bytes");
            }
        }
    }

    /// <summary>
    /// Defines the strategy to use when the circular buffer is full.
    /// </summary>
    public enum OverflowStrategy
    {
        /// <summary>
        /// Overwrite oldest messages when buffer is full.
        /// </summary>
        Overwrite,

        /// <summary>
        /// Block until space is available in the buffer.
        /// </summary>
        Block
    }

    /// <summary>
    /// Defines the scope of the IPC channel on Windows.
    /// </summary>
    public enum ScopeMode
    {
        /// <summary>
        /// Local scope - only processes in the same user session can communicate.
        /// Does not require administrator privileges.
        /// </summary>
        Local,

        /// <summary>
        /// Global scope - processes across all user sessions can communicate.
        /// May require administrator privileges on Windows.
        /// </summary>
        Global
    }
}
