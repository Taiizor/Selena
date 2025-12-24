using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Selena.Core
{
    /// <summary>
    /// Optimized synchronization using ReaderWriterLockSlim and Channel for better concurrency.
    /// </summary>
    internal class OptimizedSync : IDisposable
    {
        private readonly string _channelName;
        private readonly bool _useGlobalScope;
        private bool _disposed;

        // Reader/Writer lock for better concurrency
        private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

        // Channel for lock-free message notification
        private readonly Channel<MessageNotification> _messageChannel;

        // Windows-specific (kept for compatibility)
        private Mutex? _mutex;
        private EventWaitHandle? _dataAvailableEvent;

        public OptimizedSync(string channelName, bool useGlobalScope = false)
        {
            _channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            _useGlobalScope = useGlobalScope;

            // Create unbounded channel for message notifications
            _messageChannel = Channel.CreateUnbounded<MessageNotification>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                InitializeWindows();
            }
        }

        private void InitializeWindows()
        {
            string prefix = _useGlobalScope ? "Global\\" : "Local\\";

            try
            {
                _mutex = new Mutex(false, $"{prefix}{_channelName}_OptMutex");
                _dataAvailableEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    $"{prefix}{_channelName}_OptDataAvailable");
            }
            catch (UnauthorizedAccessException) when (_useGlobalScope)
            {
                // Fall back to local
                prefix = "Local\\";
                _mutex = new Mutex(false, $"{prefix}{_channelName}_OptMutex");
                _dataAvailableEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    $"{prefix}{_channelName}_OptDataAvailable");
            }
        }

        /// <summary>
        /// Acquires read lock for buffer access.
        /// </summary>
        public bool AcquireReadLock(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OptimizedSync));
            }

            return _rwLock.TryEnterReadLock(timeout);
        }

        /// <summary>
        /// Releases read lock.
        /// </summary>
        public void ReleaseReadLock()
        {
            if (_disposed)
            {
                return;
            }

            if (_rwLock.IsReadLockHeld)
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Acquires write lock for buffer modification.
        /// </summary>
        public bool AcquireWriteLock(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OptimizedSync));
            }

            return _rwLock.TryEnterWriteLock(timeout);
        }

        /// <summary>
        /// Releases write lock.
        /// </summary>
        public void ReleaseWriteLock()
        {
            if (_disposed)
            {
                return;
            }

            if (_rwLock.IsWriteLockHeld)
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Upgrades read lock to write lock.
        /// </summary>
        public bool TryUpgradeToWriteLock(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OptimizedSync));
            }

            if (!_rwLock.IsUpgradeableReadLockHeld)
            {
                return false;
            }

            return _rwLock.TryEnterWriteLock(timeout);
        }

        /// <summary>
        /// Signals that a message is available (lock-free).
        /// </summary>
        public async ValueTask SignalMessageAvailableAsync(MessageNotification notification)
        {
            if (_disposed)
            {
                return;
            }

            await _messageChannel.Writer.WriteAsync(notification);

            // Also signal Windows event if available
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _dataAvailableEvent?.Set();
            }
        }

        /// <summary>
        /// Waits for message notification (lock-free).
        /// </summary>
        public async ValueTask<MessageNotification?> WaitForMessageAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OptimizedSync));
            }

            try
            {
                return await _messageChannel.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets reader for message notifications.
        /// </summary>
        public ChannelReader<MessageNotification> GetMessageReader()
        {
            return _messageChannel.Reader;
        }

        /// <summary>
        /// Waits for data to become available (Windows cross-process event).
        /// </summary>
        public bool WaitForData(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OptimizedSync));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _dataAvailableEvent != null)
            {
                return _dataAvailableEvent.WaitOne(timeout);
            }

            // On Unix or if no event available, return true to check for data
            return true;
        }

        /// <summary>
        /// Legacy synchronous lock acquisition for compatibility.
        /// </summary>
        public bool AcquireLock(TimeSpan timeout)
        {
            return AcquireWriteLock(timeout);
        }

        /// <summary>
        /// Legacy lock release for compatibility.
        /// </summary>
        public void ReleaseLock()
        {
            ReleaseWriteLock();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _messageChannel.Writer.TryComplete();

            // Windows cleanup
            _dataAvailableEvent?.Dispose();
            _mutex?.Dispose();

            _rwLock?.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Notification about available message.
    /// </summary>
    public readonly struct MessageNotification(long position, int length)
    {
        public int Length { get; } = length;
        public long Position { get; } = position;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }
}
