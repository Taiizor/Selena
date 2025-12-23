using Selena.Core;
using Selena.Events;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Selena.Messaging
{
    /// <summary>
    /// Handles receiving messages from the circular buffer.
    /// </summary>
    internal class Receiver : IDisposable
    {
        private readonly CircularBuffer _buffer;
        private readonly CrossPlatformSync? _sync;
        private readonly OptimizedSync? _optimizedSync;
        private readonly int _maxWaitTime;
        private readonly bool _enableLogging;
        private readonly EventDispatcher _eventDispatcher;
        private readonly byte[] _readBuffer;
        private readonly string _channelName;
        private readonly int _dataSize; // Cached buffer data size for modulo operations

        private Thread? _receiveThread;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;
        private bool _isRunning;
        private long _localReadPosition; // Local read position for this receiver instance

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived
        {
            add => _eventDispatcher.MessageReceived += value;
            remove => _eventDispatcher.MessageReceived -= value;
        }

        public Receiver(CircularBuffer buffer, CrossPlatformSync sync, int maxWaitTime, string channelName, bool enableLogging = false)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _maxWaitTime = maxWaitTime;
            _channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            _enableLogging = enableLogging;
            _eventDispatcher = new EventDispatcher(_channelName);
            _dataSize = buffer.DataSize; // Cache the data size

            // Allocate read buffer (64KB should be enough for most messages)
            _readBuffer = new byte[65536];
        }

        public Receiver(CircularBuffer buffer, OptimizedSync optimizedSync, int maxWaitTime, string channelName, bool enableLogging = false)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _optimizedSync = optimizedSync ?? throw new ArgumentNullException(nameof(optimizedSync));
            _maxWaitTime = maxWaitTime;
            _channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            _enableLogging = enableLogging;
            _eventDispatcher = new EventDispatcher(_channelName);
            _dataSize = buffer.DataSize; // Cache the data size

            // Allocate read buffer (64KB should be enough for most messages)
            _readBuffer = new byte[65536];
        }

        /// <summary>
        /// Starts receiving messages.
        /// </summary>
        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Receiver));
            }

            if (_isRunning)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            // Initialize local read position to current write position
            // This ensures we only receive messages sent after this receiver started
            _localReadPosition = _buffer.GetCurrentWritePosition();

            // If using optimized sync, use channel-based notification
            if (_optimizedSync != null)
            {
                _receiveThread = new Thread(ReceiveThreadOptimized)
                {
                    Name = "Selena.Receiver",
                    IsBackground = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Event-driven
                _receiveThread = new Thread(ReceiveThreadWindows)
                {
                    Name = "Selena.Receiver",
                    IsBackground = true
                };
            }
            else
            {
                // Unix: Polling-based
                _sync!.DataAvailable += OnDataAvailable;
                _sync.StartListening();

                _receiveThread = new Thread(ReceiveThreadUnix)
                {
                    Name = "Selena.Receiver",
                    IsBackground = true
                };
            }

            _receiveThread.Start();

            LogInfo("Receiver started");
        }

        /// <summary>
        /// Stops receiving messages.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            if (_optimizedSync == null && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _sync!.DataAvailable -= OnDataAvailable;
                _sync.StopListening();
            }

            // Wait for thread to stop
            _receiveThread?.Join(TimeSpan.FromSeconds(5));

            _receiveThread = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            LogInfo("Receiver stopped");
        }

        private void ReceiveThreadWindows()
        {
            CancellationToken cancellationToken = _cancellationTokenSource!.Token;
            TimeSpan eventTimeout = TimeSpan.FromMilliseconds(50);
            DateTime lastPollTime = DateTime.UtcNow;
            TimeSpan pollInterval = TimeSpan.FromMilliseconds(10); // Poll every 10ms

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    bool shouldProcess = false;

                    // Wait for data available event (short timeout)
                    if (_sync!.WaitForData(eventTimeout))
                    {
                        shouldProcess = true;
                    }

                    // Also poll periodically to ensure we don't miss messages
                    // This is needed because the EventWaitHandle is AutoReset,
                    // meaning only one receiver sees each signal
                    DateTime now = DateTime.UtcNow;
                    if (now - lastPollTime >= pollInterval)
                    {
                        lastPollTime = now;
                        shouldProcess = true;
                    }

                    if (shouldProcess)
                    {
                        ProcessAvailableMessages();
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error in receive thread: {ex.Message}");
                }
            }
        }

        private void ReceiveThreadUnix()
        {
            CancellationToken cancellationToken = _cancellationTokenSource!.Token;

            // On Unix, we process messages when signaled by the polling timer
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // Just wait for cancellation
                    cancellationToken.WaitHandle.WaitOne(100);
                }
                catch (Exception ex)
                {
                    LogError($"Error in receive thread: {ex.Message}");
                }
            }
        }

        private void ReceiveThreadOptimized()
        {
            CancellationToken cancellationToken = _cancellationTokenSource!.Token;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use event-based signaling for cross-process support
                // but also poll periodically to ensure we don't miss messages
                TimeSpan eventTimeout = TimeSpan.FromMilliseconds(50);
                DateTime lastPollTime = DateTime.UtcNow;
                TimeSpan pollInterval = TimeSpan.FromMilliseconds(10); // Poll every 10ms

                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        bool shouldProcess = false;

                        // Wait for data available event (short timeout)
                        if (_optimizedSync!.WaitForData(eventTimeout))
                        {
                            shouldProcess = true;
                        }

                        // Also poll periodically to ensure we don't miss messages
                        DateTime now = DateTime.UtcNow;
                        if (now - lastPollTime >= pollInterval)
                        {
                            lastPollTime = now;
                            shouldProcess = true;
                        }

                        if (shouldProcess)
                        {
                            ProcessAvailableMessagesOptimized();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in optimized receive thread: {ex.Message}");
                    }
                }
            }
            else
            {
                // On Unix, use both channel-based notification (for same process)
                // and polling (for cross-process)
                ChannelReader<MessageNotification> reader = _optimizedSync!.GetMessageReader();
                DateTime lastPollTime = DateTime.UtcNow;
                TimeSpan pollInterval = TimeSpan.FromMilliseconds(10); // Poll every 10ms

                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        bool shouldProcess = false;

                        // Check channel for in-process notifications
                        if (reader.TryRead(out _))
                        {
                            // Drain all notifications
                            while (reader.TryRead(out _)) { }
                            shouldProcess = true;
                        }

                        // Also poll periodically for cross-process scenarios
                        DateTime now = DateTime.UtcNow;
                        if (now - lastPollTime >= pollInterval)
                        {
                            lastPollTime = now;
                            shouldProcess = true;
                        }

                        if (shouldProcess)
                        {
                            ProcessAvailableMessagesOptimized();
                        }
                        else
                        {
                            // Wait a bit before next check
                            Thread.Sleep(5);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in optimized receive thread: {ex.Message}");
                    }
                }
            }
        }

        private void OnDataAvailable(object? sender, EventArgs e)
        {
            // Called by polling timer on Unix
            ProcessAvailableMessages();
        }

        private void ProcessAvailableMessages()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(_maxWaitTime);

            // Acquire lock
            if (!_sync!.AcquireLock(timeout))
            {
                LogError("Failed to acquire lock for receiving");
                return;
            }

            try
            {
                List<Message> messages = [];

                // Read all available messages using local read position
                while (true)
                {
                    // Peek to see if we have a complete message header using local position
                    int peekedBytes = _buffer.PeekFromPosition(_readBuffer, 0, MessageHeader.Size, _localReadPosition);
                    if (peekedBytes < MessageHeader.Size)
                    {
                        break;
                    }

                    // Check header
                    if (!Serializer.TryPeekHeader(_readBuffer, 0, peekedBytes, out MessageHeader header))
                    {
                        // Invalid header, skip one byte and try again
                        _localReadPosition = (_localReadPosition + 1) % _dataSize;
                        continue;
                    }

                    // Check if we have the complete message
                    if (_buffer.GetAvailableDataFromPosition(_localReadPosition) < header.TotalLength)
                    {
                        break;
                    }

                    // Read the complete message using local position
                    int bytesRead = _buffer.ReadFromPosition(_readBuffer, 0, header.TotalLength, ref _localReadPosition);
                    if (bytesRead != header.TotalLength)
                    {
                        LogError($"Incomplete message read: expected {header.TotalLength}, got {bytesRead}");
                        break;
                    }

                    // Deserialize message
                    Message? message = Serializer.DeserializeMessage(_readBuffer, 0, bytesRead);
                    if (message != null && message.IsValid())
                    {
                        messages.Add(message);

                        if (_enableLogging)
                        {
                            LogInfo($"Received message: Type={message.Header.MessageType}, Size={message.Header.TotalLength}");
                        }
                    }
                    else
                    {
                        LogError("Failed to deserialize message");
                    }
                }

                // Dispatch messages outside of lock
                foreach (Message message in messages)
                {
                    _eventDispatcher.DispatchMessage(message);
                }
            }
            finally
            {
                _sync!.ReleaseLock();
            }
        }

        private void ProcessAvailableMessagesOptimized()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(_maxWaitTime);

            // Acquire read lock for optimized sync
            if (!_optimizedSync!.AcquireReadLock(timeout))
            {
                LogError("Failed to acquire read lock for receiving");
                return;
            }

            try
            {
                List<Message> messages = [];

                // Read all available messages using local read position
                while (true)
                {
                    // Peek to see if we have a complete message header using local position
                    int peekedBytes = _buffer.PeekFromPosition(_readBuffer, 0, MessageHeader.Size, _localReadPosition);
                    if (peekedBytes < MessageHeader.Size)
                    {
                        break;
                    }

                    // Check header
                    if (!Serializer.TryPeekHeader(_readBuffer, 0, peekedBytes, out MessageHeader header))
                    {
                        // Invalid header, skip one byte and try again
                        _localReadPosition = (_localReadPosition + 1) % _dataSize;
                        continue;
                    }

                    // Check if we have the complete message
                    if (_buffer.GetAvailableDataFromPosition(_localReadPosition) < header.TotalLength)
                    {
                        break;
                    }

                    // Read the complete message using local position
                    int bytesRead = _buffer.ReadFromPosition(_readBuffer, 0, header.TotalLength, ref _localReadPosition);
                    if (bytesRead != header.TotalLength)
                    {
                        LogError($"Incomplete message read: expected {header.TotalLength}, got {bytesRead}");
                        break;
                    }

                    // Check if this might be a compressed message
                    // Compressed messages have: MessageHeader + 4 bytes (originalSize) + 1 byte (flags) + compressed data
                    // Only messages with payload >= 5 bytes could potentially be compressed
                    // And realistically, only larger messages (>= compression threshold) would be compressed
                    bool mightBeCompressed = header.PayloadLength is >= 5 and >= 256; // Min practical compression size
                    if (mightBeCompressed)
                    {
                        // Try to read compression metadata
                        int metadataOffset = MessageHeader.Size;
                        if (bytesRead >= metadataOffset + 5)
                        {
                            int originalSize = BitConverter.ToInt32(_readBuffer, metadataOffset);
                            byte flags = _readBuffer[metadataOffset + 4];
                            bool compressed = (flags & 1) != 0;

                            // Validate that this looks like a real compressed message
                            // originalSize should be reasonable (positive and not too large)
                            if (compressed && originalSize > 0 && originalSize < 100 * 1024 * 1024) // Max 100MB uncompressed
                            {
                                try
                                {
                                    // Decompress the payload
                                    int compressedPayloadOffset = metadataOffset + 5;
                                    int compressedPayloadLength = header.PayloadLength - 5;

                                    if (compressedPayloadLength > 0)
                                    {
                                        ReadOnlySpan<byte> compressedData = new(_readBuffer, compressedPayloadOffset, compressedPayloadLength);
                                        byte[] decompressedPayload = CompressionManager.Decompress(compressedData, originalSize);

                                        // Create message with decompressed payload
                                        Message decompressedMessage = new(decompressedPayload, header.MessageType);
                                        messages.Add(decompressedMessage);

                                        if (_enableLogging)
                                        {
                                            LogInfo($"Received message: Type={decompressedMessage.Header.MessageType}, Size={decompressedMessage.Header.TotalLength}, Decompressed from {header.TotalLength}");
                                        }
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError($"Failed to decompress message: {ex.Message}");
                                    // Fall through to deserialize as regular message
                                }
                            }
                        }
                    }

                    // Deserialize message normally (not compressed or decompression failed)
                    Message? message = Serializer.DeserializeMessage(_readBuffer, 0, bytesRead);
                    if (message != null && message.IsValid())
                    {
                        messages.Add(message);

                        if (_enableLogging)
                        {
                            LogInfo($"Received message: Type={message.Header.MessageType}, Size={message.Header.TotalLength}");
                        }
                    }
                    else
                    {
                        LogError("Failed to deserialize message");
                    }
                }

                // Dispatch messages outside of lock
                foreach (Message message in messages)
                {
                    _eventDispatcher.DispatchMessage(message);
                }
            }
            finally
            {
                _optimizedSync.ReleaseReadLock();
            }
        }

        /// <summary>
        /// Receives a single message synchronously.
        /// </summary>
        public Message? ReceiveMessage(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Receiver));
            }

            DateTime startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                bool lockAcquired;
                if (_optimizedSync != null)
                {
                    lockAcquired = _optimizedSync.AcquireReadLock(TimeSpan.FromMilliseconds(100));
                }
                else
                {
                    lockAcquired = _sync!.AcquireLock(TimeSpan.FromMilliseconds(100));
                }

                if (!lockAcquired)
                {
                    continue;
                }

                try
                {
                    // Check if we have a message using local read position
                    int peekedBytes = _buffer.PeekFromPosition(_readBuffer, 0, MessageHeader.Size, _localReadPosition);
                    if (peekedBytes < MessageHeader.Size)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // Check header
                    if (!Serializer.TryPeekHeader(_readBuffer, 0, peekedBytes, out MessageHeader header))
                    {
                        // Invalid header, skip one byte
                        _localReadPosition = (_localReadPosition + 1) % _dataSize;
                        continue;
                    }

                    // Check if we have the complete message
                    if (_buffer.GetAvailableDataFromPosition(_localReadPosition) < header.TotalLength)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // Read the complete message using local position
                    int bytesRead = _buffer.ReadFromPosition(_readBuffer, 0, header.TotalLength, ref _localReadPosition);
                    if (bytesRead != header.TotalLength)
                    {
                        continue;
                    }

                    // Deserialize and return
                    Message? message = Serializer.DeserializeMessage(_readBuffer, 0, bytesRead);
                    if (message != null && message.IsValid())
                    {
                        return message;
                    }
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

                Thread.Sleep(10);
            }

            return null;
        }

        /// <summary>
        /// Receives a single message asynchronously.
        /// </summary>
        public async Task<Message?> ReceiveMessageAsync(TimeSpan timeout)
        {
            return await Task.Run(() => ReceiveMessage(timeout));
        }

        private void LogInfo(string message)
        {
            if (_enableLogging)
            {
                Console.WriteLine($"[Selena.Receiver] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} INFO: {message}");
            }
        }

        private void LogError(string message)
        {
            if (_enableLogging)
            {
                Console.WriteLine($"[Selena.Receiver] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} ERROR: {message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _eventDispatcher.Dispose();
            _disposed = true;
        }
    }
}
