using Selena.Core;

namespace Selena.Messaging
{
    /// <summary>
    /// Handles sending messages through the circular buffer.
    /// </summary>
    internal class Sender : IDisposable
    {
        private readonly CircularBuffer _buffer;
        private readonly CrossPlatformSync _sync;
        private readonly int _maxWaitTime;
        private readonly bool _enableLogging;
        private bool _disposed;

        public Sender(CircularBuffer buffer, CrossPlatformSync sync, int maxWaitTime, bool enableLogging = false)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _maxWaitTime = maxWaitTime;
            _enableLogging = enableLogging;
        }

        /// <summary>
        /// Sends a message.
        /// </summary>
        public bool SendMessage(Message message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Sender));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!message.IsValid())
            {
                throw new ArgumentException("Invalid message", nameof(message));
            }

            // Serialize message
            byte[] messageBytes = Serializer.SerializeMessage(message);

            // Acquire lock
            TimeSpan timeout = TimeSpan.FromMilliseconds(_maxWaitTime);
            if (!_sync.AcquireLock(timeout))
            {
                LogError("Failed to acquire lock for sending");
                return false;
            }

            try
            {
                // Write to buffer
                bool written = _buffer.Write(messageBytes, 0, messageBytes.Length, timeout);

                if (written)
                {
                    // Signal data available
                    _sync.SignalDataAvailable();

                    if (_enableLogging)
                    {
                        LogInfo($"Sent message: Type={message.Header.MessageType}, Size={message.Header.TotalLength}");
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
                _sync.ReleaseLock();
            }
        }

        /// <summary>
        /// Sends a message asynchronously.
        /// </summary>
        public async Task<bool> SendMessageAsync(Message message)
        {
            return await Task.Run(() => SendMessage(message));
        }

        /// <summary>
        /// Sends a text message.
        /// </summary>
        public bool SendTextMessage(string text, int messageType = 0)
        {
            Message message = Serializer.CreateTextMessage(text, messageType);
            return SendMessage(message);
        }

        /// <summary>
        /// Sends a text message asynchronously.
        /// </summary>
        public async Task<bool> SendTextMessageAsync(string text, int messageType = 0)
        {
            Message message = Serializer.CreateTextMessage(text, messageType);
            return await SendMessageAsync(message);
        }

        /// <summary>
        /// Sends an object as JSON.
        /// </summary>
        public bool SendObject<T>(T obj, int messageType = 0)
        {
            Message message = Serializer.CreateJsonMessage(obj, messageType);
            return SendMessage(message);
        }

        /// <summary>
        /// Sends an object as JSON asynchronously.
        /// </summary>
        public async Task<bool> SendObjectAsync<T>(T obj, int messageType = 0)
        {
            Message message = Serializer.CreateJsonMessage(obj, messageType);
            return await SendMessageAsync(message);
        }

        /// <summary>
        /// Sends raw bytes.
        /// </summary>
        public bool SendBytes(byte[] data, int messageType = 0)
        {
            Message message = new(data, messageType);
            return SendMessage(message);
        }

        /// <summary>
        /// Sends raw bytes asynchronously.
        /// </summary>
        public async Task<bool> SendBytesAsync(byte[] data, int messageType = 0)
        {
            Message message = new(data, messageType);
            return await SendMessageAsync(message);
        }

        /// <summary>
        /// Broadcasts a message multiple times for redundancy.
        /// </summary>
        public async Task<int> BroadcastMessageAsync(Message message, int count, int delayMs = 10)
        {
            int successCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (await SendMessageAsync(message))
                {
                    successCount++;
                }

                if (i < count - 1 && delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }
            }

            return successCount;
        }

        private void LogInfo(string message)
        {
            if (_enableLogging)
            {
                Console.WriteLine($"[Selena.Sender] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} INFO: {message}");
            }
        }

        private void LogError(string message)
        {
            if (_enableLogging)
            {
                Console.WriteLine($"[Selena.Sender] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} ERROR: {message}");
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
