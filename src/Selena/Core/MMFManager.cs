using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Selena.Core
{
    /// <summary>
    /// Manages Memory-Mapped Files for cross-platform inter-process communication.
    /// </summary>
    internal class MMFManager : IDisposable
    {
        private readonly string _channelName;
        private readonly string _mmfFilePath;
        private readonly bool _useGlobalScope;
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private bool _disposed;

        public MMFManager(string channelName, int bufferSize, bool useGlobalScope = false)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                throw new ArgumentException("Channel name cannot be empty", nameof(channelName));
            }

            if (bufferSize < 1024)
            {
                throw new ArgumentException("Buffer size must be at least 1KB", nameof(bufferSize));
            }

            _channelName = channelName;
            BufferSize = bufferSize;
            _useGlobalScope = useGlobalScope;

            // Create file-backed MMF path for cross-platform compatibility
            _mmfFilePath = GetMMFFilePath(channelName);
        }

        /// <summary>
        /// Gets the accessor for the memory-mapped file.
        /// </summary>
        public MemoryMappedViewAccessor Accessor
        {
            get
            {
                if (_accessor == null)
                {
                    throw new InvalidOperationException("MMF not initialized. Call CreateOrOpen first.");
                }

                return _accessor;
            }
        }

        /// <summary>
        /// Gets the buffer size.
        /// </summary>
        public int BufferSize { get; }

        /// <summary>
        /// Creates or opens the memory-mapped file.
        /// </summary>
        public void CreateOrOpen()
        {
            try
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(_mmfFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create or open the backing file
                using (FileStream fs = new(_mmfFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    if (fs.Length < BufferSize)
                    {
                        fs.SetLength(BufferSize);
                    }
                }

                // Create MMF from file
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // On Windows, try to use named MMF for better performance
                    try
                    {
                        string prefix = _useGlobalScope ? "Global\\" : "Local\\";
                        _mmf = MemoryMappedFile.CreateOrOpen(
                            $"{prefix}{_channelName}",
                            BufferSize,
                            MemoryMappedFileAccess.ReadWrite,
                            MemoryMappedFileOptions.None,
                            HandleInheritability.None);
                    }
                    catch (UnauthorizedAccessException) when (_useGlobalScope)
                    {
                        // Try with Local prefix if Global access is denied
                        try
                        {
                            _mmf = MemoryMappedFile.CreateOrOpen(
                                $"Local\\{_channelName}",
                                BufferSize,
                                MemoryMappedFileAccess.ReadWrite,
                                MemoryMappedFileOptions.None,
                                HandleInheritability.None);
                        }
                        catch
                        {
                            // Fall back to file-backed MMF
                            _mmf = MemoryMappedFile.CreateFromFile(
                                _mmfFilePath,
                                FileMode.Open,
                                null,
                                BufferSize,
                                MemoryMappedFileAccess.ReadWrite);
                        }
                    }
                    catch
                    {
                        // Fall back to file-backed MMF
                        _mmf = MemoryMappedFile.CreateFromFile(
                            _mmfFilePath,
                            FileMode.Open,
                            null,
                            BufferSize,
                            MemoryMappedFileAccess.ReadWrite);
                    }
                }
                else
                {
                    // On Linux/macOS, use file-backed MMF
                    _mmf = MemoryMappedFile.CreateFromFile(
                        _mmfFilePath,
                        FileMode.Open,
                        null,
                        BufferSize,
                        MemoryMappedFileAccess.ReadWrite);
                }

                _accessor = _mmf.CreateViewAccessor(0, BufferSize, MemoryMappedFileAccess.ReadWrite);

                // Initialize buffer header if needed
                InitializeBufferIfNeeded();
            }
            catch (Exception ex)
            {
                Dispose();
                throw new InvalidOperationException($"Failed to create/open MMF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Closes the memory-mapped file.
        /// </summary>
        public void Close()
        {
            _accessor?.Dispose();
            _accessor = null;

            _mmf?.Dispose();
            _mmf = null;
        }

        /// <summary>
        /// Disposes the MMFManager resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Close();
            _disposed = true;
        }

        /// <summary>
        /// Gets the cross-platform file path for the MMF backing file.
        /// </summary>
        private static string GetMMFFilePath(string channelName)
        {
            string baseDir;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Selena");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                baseDir = "/dev/shm"; // Use shared memory on Linux
                if (!Directory.Exists(baseDir))
                {
                    baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Selena");
                }
            }
            else // macOS
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Selena");
            }

            return Path.Combine(baseDir, $"{channelName}.mmf");
        }

        /// <summary>
        /// Initializes the buffer header if it's a new buffer.
        /// </summary>
        private void InitializeBufferIfNeeded()
        {
            const int MAGIC_NUMBER = 0x53454C4E; // 'SELN' in hex

            int magic = _accessor!.ReadInt32(0);
            if (magic != MAGIC_NUMBER)
            {
                // Initialize buffer header
                _accessor.Write(0, MAGIC_NUMBER);
                _accessor.Write(4, 1); // Version
                _accessor.Write(8, BufferSize);
                _accessor.Write(16, 0L); // Write position
                _accessor.Write(24, 0L); // Read position
            }
        }
    }
}
