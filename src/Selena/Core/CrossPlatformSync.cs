using System.Runtime.InteropServices;

namespace Selena.Core
{
    /// <summary>
    /// Provides cross-platform synchronization primitives for inter-process communication.
    /// </summary>
    internal class CrossPlatformSync : IDisposable
    {
        private readonly string _channelName;
        private readonly int _pollingInterval;
        private readonly bool _useGlobalScope;
        private bool _disposed;

        // Windows-specific
        private Mutex? _mutex;
        private EventWaitHandle? _dataAvailableEvent;

        // Cross-platform file-based locking
        private readonly object _localLock = new();
        private Timer? _pollingTimer;

        // Event for data available notification
        public event EventHandler? DataAvailable;

        public CrossPlatformSync(string channelName, int pollingInterval, bool useGlobalScope = false)
        {
            _channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            _pollingInterval = pollingInterval;
            _useGlobalScope = useGlobalScope;

            Initialize();
        }

        private void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InitializeWindows();
            }
            else
            {
                InitializeUnix();
            }
        }

        private void InitializeWindows()
        {
            string prefix = _useGlobalScope ? "Global\\" : "Local\\";

            try
            {
                // Create named mutex for cross-process synchronization
                _mutex = new Mutex(false, $"{prefix}{_channelName}_Mutex");

                // Create named event for signaling data availability
                _dataAvailableEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    $"{prefix}{_channelName}_DataAvailable");
            }
            catch (UnauthorizedAccessException) when (_useGlobalScope)
            {
                // If global access is denied, fall back to local
                prefix = "Local\\";
                _mutex = new Mutex(false, $"{prefix}{_channelName}_Mutex");
                _dataAvailableEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    $"{prefix}{_channelName}_DataAvailable");
            }
        }

        private void InitializeUnix()
        {
            // On Unix, we rely on MMF's own file locking and local synchronization
            // Start polling timer for data availability checking
            _pollingTimer = new Timer(
                CheckDataAvailable,
                null,
                _pollingInterval,
                _pollingInterval);
        }

        /// <summary>
        /// Acquires exclusive lock for buffer access.
        /// </summary>
        public bool AcquireLock(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CrossPlatformSync));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return _mutex!.WaitOne(timeout);
                }
                catch (AbandonedMutexException)
                {
                    // The mutex was abandoned, but we now own it
                    return true;
                }
            }
            else
            {
                // On Unix, use local locking
                // Note: This provides thread-safety but not true cross-process locking
                // For production use, consider using named semaphores via P/Invoke
                return Monitor.TryEnter(_localLock, timeout);
            }
        }

        /// <summary>
        /// Releases exclusive lock.
        /// </summary>
        public void ReleaseLock()
        {
            if (_disposed)
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _mutex?.ReleaseMutex();
            }
            else
            {
                // Release local lock on Unix
                if (Monitor.IsEntered(_localLock))
                {
                    Monitor.Exit(_localLock);
                }
            }
        }

        /// <summary>
        /// Signals that data is available (Windows only, no-op on Unix).
        /// </summary>
        public void SignalDataAvailable()
        {
            if (_disposed)
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _dataAvailableEvent?.Set();
            }
            // On Unix, the polling timer will detect new data
        }

        /// <summary>
        /// Waits for data to become available.
        /// </summary>
        public bool WaitForData(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CrossPlatformSync));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return _dataAvailableEvent!.WaitOne(timeout);
            }
            else
            {
                // On Unix, we rely on polling and event callbacks
                // This method is primarily used on Windows
                return true;
            }
        }

        /// <summary>
        /// Starts listening for data (Unix only, triggers polling).
        /// </summary>
        public void StartListening()
        {
            // On Windows, event-driven model is used
            // On Unix, polling is already started in Initialize
        }

        /// <summary>
        /// Stops listening for data.
        /// </summary>
        public void StopListening()
        {
            _pollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void CheckDataAvailable(object? state)
        {
            // This is called periodically on Unix systems
            // The actual data checking is handled by the receiver
            // We just trigger the event to check for new data
            DataAvailable?.Invoke(this, EventArgs.Empty);
        }


        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopListening();

            // Windows cleanup
            _dataAvailableEvent?.Dispose();
            _mutex?.Dispose();

            // Unix cleanup
            _pollingTimer?.Dispose();

            _disposed = true;
        }
    }
}
