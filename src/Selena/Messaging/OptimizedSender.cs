using Selena.Core;
using System.Text;
using System.Text.Json;

namespace Selena.Messaging
{
    /// <summary>
    /// Optimized sender with memory pooling, compression, and improved locking.
    /// </summary>
    internal class OptimizedSender(
        CircularBuffer buffer,
        OptimizedSync sync,
        int maxWaitTime,
        bool enableLogging = false,
        bool enableCompression = true,
        int compressionThreshold = 1024) : IDisposable
    {
        private bool _disposed;
        private readonly OptimizedSync _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        private readonly CircularBuffer _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

        /// <summary>
        /// Sends an optimized message with memory pooling.
        /// </summary>
        public async Task<bool> SendMessageAsync(OptimizedMessage message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OptimizedSender));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!message.IsValid())
            {
                throw new ArgumentException("Invalid message", nameof(message));
            }

            // Determine if compression should be used
            bool shouldCompress = enableCompression && message.Payload.Length >= compressionThreshold;

            PooledBuffer? compressedBuffer = null;
            byte[]? messageBytes = null;

            try
            {
                if (shouldCompress)
                {
                    // Compress payload
                    compressedBuffer = CompressionManager.CompressPooled(message.Payload.Span);

                    // Create compressed header
                    CompressedMessageHeader compressedHeader = CompressedMessageHeader.Create(
                        compressedBuffer.Value.Length,
                        message.Header.MessageType,
                        true,
                        message.Payload.Length);

                    // Serialize with compressed data
                    messageBytes = SerializeCompressedMessage(compressedHeader, compressedBuffer.Value.Span);
                }
                else
                {
                    // Serialize without compression
                    messageBytes = SerializeMessage(message);
                }

                // Acquire write lock with timeout
                TimeSpan timeout = TimeSpan.FromMilliseconds(maxWaitTime);
                if (!_sync.AcquireWriteLock(timeout))
                {
                    LogError("Failed to acquire write lock for sending");
                    return false;
                }

                try
                {
                    // Write to buffer
                    bool written = _buffer.Write(messageBytes, 0, messageBytes.Length, timeout);

                    if (written)
                    {
                        // Signal data available asynchronously
                        MessageNotification notification = new(
                            _buffer.GetAvailableData() - messageBytes.Length,
                            messageBytes.Length);

                        await _sync.SignalMessageAvailableAsync(notification);

                        if (enableLogging)
                        {
                            LogInfo($"Sent message: Type={message.Header.MessageType}, " +
                                   $"Size={message.Header.TotalLength}, " +
                                   $"Compressed={shouldCompress}");
                        }
                    }
                    else
                    {
                        LogError("Failed to write message to buffer");
                    }

                    return written;
                }
                finally
                {
                    _sync.ReleaseWriteLock();
                }
            }
            finally
            {
                // Return buffers to pool
                compressedBuffer?.Dispose();
                if (messageBytes != null)
                {
                    BufferPoolManager.ReturnBuffer(messageBytes);
                }
            }
        }

        /// <summary>
        /// Sends raw bytes with automatic pooling.
        /// </summary>
        public async Task<bool> SendBytesAsync(byte[] data, int messageType = 0)
        {
            using OptimizedMessage message = new(data, messageType);
            return await SendMessageAsync(message);
        }

        /// <summary>
        /// Sends text message with automatic pooling.
        /// </summary>
        public async Task<bool> SendTextMessageAsync(string text, int messageType = 0)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return await SendBytesAsync(bytes, messageType);
        }

        /// <summary>
        /// Sends object as JSON with automatic pooling.
        /// </summary>
        public async Task<bool> SendObjectAsync<T>(T obj, int messageType = 0)
        {
            string json = JsonSerializer.Serialize(obj);
            return await SendTextMessageAsync(json, messageType);
        }

        /// <summary>
        /// Batch send multiple messages efficiently.
        /// </summary>
        public async Task<int> SendBatchAsync(IEnumerable<OptimizedMessage> messages)
        {
            int successCount = 0;
            TimeSpan timeout = TimeSpan.FromMilliseconds(maxWaitTime);

            // Acquire write lock once for the entire batch
            if (!_sync.AcquireWriteLock(timeout))
            {
                LogError("Failed to acquire write lock for batch send");
                return 0;
            }

            try
            {
                List<MessageNotification> notifications = [];

                foreach (OptimizedMessage message in messages)
                {
                    byte[] messageBytes = SerializeMessage(message);

                    if (_buffer.Write(messageBytes, 0, messageBytes.Length, TimeSpan.Zero))
                    {
                        successCount++;
                        notifications.Add(new MessageNotification(
                            _buffer.GetAvailableData() - messageBytes.Length,
                            messageBytes.Length));
                    }

                    BufferPoolManager.ReturnBuffer(messageBytes);
                }

                // Signal all notifications
                foreach (MessageNotification notification in notifications)
                {
                    await _sync.SignalMessageAvailableAsync(notification);
                }
            }
            finally
            {
                _sync.ReleaseWriteLock();
            }

            return successCount;
        }

        private byte[] SerializeMessage(OptimizedMessage message)
        {
            byte[] buffer = BufferPoolManager.RentBuffer(message.GetTotalSize());

            // Serialize header
            Serializer.SerializeHeader(message.Header, buffer, 0);

            // Copy payload
            if (message.Payload.Length > 0)
            {
                message.Payload.CopyTo(new Memory<byte>(buffer, MessageHeader.Size, message.Payload.Length));
            }

            return buffer;
        }

        private byte[] SerializeCompressedMessage(CompressedMessageHeader header, ReadOnlySpan<byte> compressedData)
        {
            int totalSize = MessageHeader.Size + 5 + compressedData.Length;
            byte[] buffer = BufferPoolManager.RentBuffer(totalSize);

            // Serialize base header
            Serializer.SerializeHeader(header.BaseHeader, buffer, 0);

            // Serialize compression info
            byte[] originalSizeBytes = BitConverter.GetBytes(header.OriginalSize);
            Buffer.BlockCopy(originalSizeBytes, 0, buffer, MessageHeader.Size, 4);
            buffer[MessageHeader.Size + 4] = header.Flags;

            // Copy compressed payload  
            byte[] compressedArray = compressedData.ToArray();
            Buffer.BlockCopy(compressedArray, 0, buffer, CompressedMessageHeader.Size, compressedArray.Length);

            return buffer;
        }

        private void LogInfo(string message)
        {
            if (enableLogging)
            {
                Console.WriteLine($"[Selena.OptimizedSender] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} INFO: {message}");
            }
        }

        private void LogError(string message)
        {
            if (enableLogging)
            {
                Console.WriteLine($"[Selena.OptimizedSender] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} ERROR: {message}");
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
