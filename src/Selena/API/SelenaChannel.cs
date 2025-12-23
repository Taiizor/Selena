using Selena.Core;
using Selena.Events;
using Selena.Messaging;

namespace Selena.API
{
    /// <summary>
    /// Main API for Selena inter-process communication channel.
    /// </summary>
    public class SelenaChannel : IDisposable
    {
        private readonly MMFManager _mmfManager;
        private readonly CircularBuffer _buffer;
        private readonly CrossPlatformSync? _sync;
        private readonly OptimizedSync? _optimizedSync;
        private readonly Sender? _sender;
        private readonly OptimizedSender? _optimizedSender;
        private readonly Receiver _receiver;
        private readonly bool _useOptimized;
        private bool _disposed;

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived
        {
            add => _receiver.MessageReceived += value;
            remove => _receiver.MessageReceived -= value;
        }

        /// <summary>
        /// Creates a new Selena channel with default configuration.
        /// </summary>
        public SelenaChannel(string channelName) : this(new SelenaConfig { ChannelName = channelName })
        {
        }

        /// <summary>
        /// Creates a new Selena channel with the specified configuration.
        /// </summary>
        public SelenaChannel(SelenaConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Config.Validate();

            _useOptimized = Config.EnableOptimizations;

            try
            {
                // Initialize core components
                bool useGlobalScope = Config.ScopeMode == ScopeMode.Global;
                _mmfManager = new MMFManager(Config.ChannelName, Config.BufferSize, useGlobalScope);
                _mmfManager.CreateOrOpen();

                _buffer = new CircularBuffer(_mmfManager.Accessor, Config.BufferSize, Config.OverflowStrategy);

                // Initialize synchronization based on configuration
                if (_useOptimized && Config.UseReaderWriterLocks)
                {
                    _optimizedSync = new OptimizedSync(Config.ChannelName, useGlobalScope);

                    // Initialize optimized sender
                    _optimizedSender = new OptimizedSender(
                        _buffer,
                        _optimizedSync,
                        Config.MaxWaitTime,
                        Config.EnableJsonLogging,
                        Config.EnableCompression,
                        Config.CompressionThreshold);

                    // Initialize receiver with optimized sync
                    _receiver = new Receiver(_buffer, _optimizedSync, Config.MaxWaitTime, Config.ChannelName, Config.EnableJsonLogging);
                }
                else
                {
                    // Use legacy components
                    _sync = new CrossPlatformSync(Config.ChannelName, Config.PollingInterval, useGlobalScope);
                    _sender = new Sender(_buffer, _sync, Config.MaxWaitTime, Config.EnableJsonLogging);
                    _receiver = new Receiver(_buffer, _sync, Config.MaxWaitTime, Config.ChannelName, Config.EnableJsonLogging);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets the channel configuration.
        /// </summary>
        public SelenaConfig Config { get; }

        /// <summary>
        /// Gets whether the channel is started and listening for messages.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Starts listening for incoming messages.
        /// </summary>
        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (IsStarted)
            {
                return;
            }

            _receiver.Start();
            IsStarted = true;
        }

        /// <summary>
        /// Stops listening for incoming messages.
        /// </summary>
        public void Stop()
        {
            if (!IsStarted)
            {
                return;
            }

            _receiver.Stop();
            IsStarted = false;
        }

        /// <summary>
        /// Sends a text message.
        /// </summary>
        public bool SendMessage(string text, int messageType = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_useOptimized && _optimizedSender != null)
            {
                return _optimizedSender.SendTextMessageAsync(text, messageType).GetAwaiter().GetResult();
            }

            if (_sender == null)
            {
                throw new InvalidOperationException("Sender is not initialized");
            }

            return _sender.SendTextMessage(text, messageType);
        }

        /// <summary>
        /// Sends a text message asynchronously.
        /// </summary>
        public async Task<bool> SendMessageAsync(string text, int messageType = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_useOptimized && _optimizedSender != null)
            {
                return await _optimizedSender.SendTextMessageAsync(text, messageType);
            }

            return await _sender!.SendTextMessageAsync(text, messageType);
        }

        /// <summary>
        /// Sends raw bytes.
        /// </summary>
        public bool SendBytes(byte[] data, int messageType = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_useOptimized && _optimizedSender != null)
            {
                return _optimizedSender.SendBytesAsync(data, messageType).GetAwaiter().GetResult();
            }

            if (_sender == null)
            {
                throw new InvalidOperationException("Sender is not initialized");
            }

            return _sender.SendBytes(data, messageType);
        }

        /// <summary>
        /// Sends raw bytes asynchronously.
        /// </summary>
        public async Task<bool> SendBytesAsync(byte[] data, int messageType = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_useOptimized && _optimizedSender != null)
            {
                return await _optimizedSender.SendBytesAsync(data, messageType);
            }

            return await _sender!.SendBytesAsync(data, messageType);
        }

        /// <summary>
        /// Sends an object as JSON.
        /// </summary>
        public bool SendObject<T>(T obj, int messageType = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_useOptimized && _optimizedSender != null)
            {
                return _optimizedSender.SendObjectAsync(obj, messageType).GetAwaiter().GetResult();
            }

            if (_sender == null)
            {
                throw new InvalidOperationException("Sender is not initialized");
            }

            return _sender.SendObject(obj, messageType);
        }

        /// <summary>
        /// Sends an object as JSON asynchronously.
        /// </summary>
        public async Task<bool> SendObjectAsync<T>(T obj, int messageType = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_useOptimized && _optimizedSender != null)
            {
                return await _optimizedSender.SendObjectAsync(obj, messageType);
            }

            return await _sender!.SendObjectAsync(obj, messageType);
        }

        /// <summary>
        /// Sends a Message object.
        /// </summary>
        public bool SendMessage(Message message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_sender == null)
            {
                throw new InvalidOperationException("Sender is not initialized");
            }

            return _sender.SendMessage(message);
        }

        /// <summary>
        /// Sends a Message object asynchronously.
        /// </summary>
        public async Task<bool> SendMessageAsync(Message message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_sender == null)
            {
                throw new InvalidOperationException("Sender is not initialized");
            }

            return await _sender.SendMessageAsync(message);
        }

        /// <summary>
        /// Receives a single message synchronously.
        /// </summary>
        public Message? ReceiveMessage(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            return _receiver.ReceiveMessage(timeout);
        }

        /// <summary>
        /// Receives a single message asynchronously.
        /// </summary>
        public async Task<Message?> ReceiveMessageAsync(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            return await _receiver.ReceiveMessageAsync(timeout);
        }

        /// <summary>
        /// Broadcasts a message multiple times for redundancy.
        /// </summary>
        public async Task<int> BroadcastMessageAsync(string text, int count, int messageType = 0, int delayMs = 10)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            if (_sender == null)
            {
                throw new InvalidOperationException("Sender is not initialized");
            }

            Message message = Serializer.CreateTextMessage(text, messageType);
            return await _sender.BroadcastMessageAsync(message, count, delayMs);
        }

        /// <summary>
        /// Clears all messages in the buffer.
        /// </summary>
        public void ClearBuffer()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            TimeSpan timeout = TimeSpan.FromMilliseconds(Config.MaxWaitTime);
            bool lockAcquired;

            if (_optimizedSync != null)
            {
                lockAcquired = _optimizedSync.AcquireWriteLock(timeout);
            }
            else if (_sync != null)
            {
                lockAcquired = _sync.AcquireLock(timeout);
            }
            else
            {
                throw new InvalidOperationException("Sync is not initialized");
            }

            if (lockAcquired)
            {
                try
                {
                    _buffer.Reset();
                }
                finally
                {
                    if (_optimizedSync != null)
                    {
                        _optimizedSync.ReleaseWriteLock();
                    }
                    else
                    {
                        _sync!.ReleaseLock();
                    }
                }
            }
        }

        /// <summary>
        /// Gets statistics about the channel.
        /// </summary>
        public ChannelStatistics GetStatistics()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelenaChannel));
            }

            ChannelStatistics stats = new()
            {
                ChannelName = Config.ChannelName,
                BufferSize = Config.BufferSize,
                IsStarted = IsStarted
            };

            TimeSpan timeout = TimeSpan.FromMilliseconds(100);
            bool lockAcquired;

            if (_optimizedSync != null)
            {
                lockAcquired = _optimizedSync.AcquireReadLock(timeout);
            }
            else if (_sync != null)
            {
                lockAcquired = _sync.AcquireLock(timeout);
            }
            else
            {
                throw new InvalidOperationException("Sync is not initialized");
            }

            if (lockAcquired)
            {
                try
                {
                    stats.AvailableData = _buffer.GetAvailableData();
                }
                finally
                {
                    if (_optimizedSync != null)
                    {
                        _optimizedSync.ReleaseReadLock();
                    }
                    else
                    {
                        _sync!.ReleaseLock();
                    }
                }
            }

            return stats;
        }

        /// <summary>
        /// Disposes the Selena channel.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();

            _receiver?.Dispose();
            _sender?.Dispose();
            _optimizedSender?.Dispose();
            _sync?.Dispose();
            _optimizedSync?.Dispose();
            _mmfManager?.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Channel statistics.
    /// </summary>
    public class ChannelStatistics
    {
        public string ChannelName { get; set; } = "";
        public int BufferSize { get; set; }
        public long AvailableData { get; set; }
        public bool IsStarted { get; set; }

        public override string ToString()
        {
            return $"Channel: {ChannelName}, Buffer: {BufferSize:N0} bytes, Available: {AvailableData:N0} bytes, Started: {IsStarted}";
        }
    }
}
