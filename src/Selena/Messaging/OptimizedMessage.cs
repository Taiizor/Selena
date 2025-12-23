namespace Selena.Messaging
{
    /// <summary>
    /// Optimized message structure using Memory&lt;byte&gt; for better performance.
    /// </summary>
    public class OptimizedMessage : IDisposable
    {
        private byte[]? _rentedBuffer;
        private bool _disposed;

        /// <summary>
        /// Message header containing metadata.
        /// </summary>
        public MessageHeader Header { get; set; }

        /// <summary>
        /// Message payload data as Memory&lt;byte&gt;.
        /// </summary>
        public Memory<byte> Payload { get; private set; }

        /// <summary>
        /// Creates a new optimized message.
        /// </summary>
        public OptimizedMessage()
        {
            Payload = Memory<byte>.Empty;
        }

        /// <summary>
        /// Creates a new optimized message with the specified payload.
        /// </summary>
        public OptimizedMessage(ReadOnlySpan<byte> payload, int messageType)
        {
            _rentedBuffer = Core.BufferPoolManager.RentBuffer(payload.Length);
            payload.CopyTo(_rentedBuffer);
            Payload = new Memory<byte>(_rentedBuffer, 0, payload.Length);
            Header = MessageHeader.Create(payload.Length, messageType);
        }

        /// <summary>
        /// Creates a new optimized message from existing buffer (no copy).
        /// </summary>
        internal OptimizedMessage(byte[] buffer, int offset, int length, MessageHeader header)
        {
            _rentedBuffer = buffer;
            Payload = new Memory<byte>(buffer, offset, length);
            Header = header;
        }

        /// <summary>
        /// Gets the timestamp as DateTime.
        /// </summary>
        public DateTime GetTimestamp()
        {
            return new DateTime(Header.Timestamp, DateTimeKind.Utc);
        }

        /// <summary>
        /// Gets the total size of the message including header.
        /// </summary>
        public int GetTotalSize()
        {
            return Header.TotalLength;
        }

        /// <summary>
        /// Validates the message.
        /// </summary>
        public bool IsValid()
        {
            return Header.IsValid() &&
                   Payload.Length == Header.PayloadLength;
        }

        /// <summary>
        /// Converts to legacy Message format for compatibility.
        /// </summary>
        public Message ToMessage()
        {
            return new Message
            {
                Header = Header,
                Payload = Payload.ToArray()
            };
        }

        /// <summary>
        /// Creates from legacy Message format.
        /// </summary>
        public static OptimizedMessage FromMessage(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new OptimizedMessage(message.Payload, message.Header.MessageType);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_rentedBuffer != null)
                {
                    Core.BufferPoolManager.ReturnBuffer(_rentedBuffer);
                    _rentedBuffer = null;
                }
                Payload = Memory<byte>.Empty;
                _disposed = true;
            }
        }
    }
}
