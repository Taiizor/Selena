using System.Runtime.InteropServices;

namespace Selena.Messaging
{
    /// <summary>
    /// Message header structure for efficient serialization.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MessageHeader
    {
        /// <summary>
        /// Total length of the message including header.
        /// </summary>
        public int TotalLength;

        /// <summary>
        /// Length of the payload in bytes.
        /// </summary>
        public int PayloadLength;

        /// <summary>
        /// Message type identifier.
        /// </summary>
        public int MessageType;

        /// <summary>
        /// UTC timestamp in ticks.
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        public int Reserved;

        /// <summary>
        /// Size of the header in bytes.
        /// </summary>
        public const int Size = 24; // 4 + 4 + 4 + 8 + 4

        /// <summary>
        /// Creates a new message header.
        /// </summary>
        public static MessageHeader Create(int payloadLength, int messageType)
        {
            return new MessageHeader
            {
                TotalLength = Size + payloadLength,
                PayloadLength = payloadLength,
                MessageType = messageType,
                Timestamp = DateTime.UtcNow.Ticks,
                Reserved = 0
            };
        }

        /// <summary>
        /// Validates the header.
        /// </summary>
        public bool IsValid()
        {
            return TotalLength >= Size &&
                   PayloadLength >= 0 &&
                   TotalLength == Size + PayloadLength;
        }
    }

    /// <summary>
    /// Represents a message for inter-process communication.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Message header containing metadata.
        /// </summary>
        public MessageHeader Header { get; set; }

        /// <summary>
        /// Message payload data.
        /// </summary>
        public byte[] Payload { get; set; }

        /// <summary>
        /// Creates a new message.
        /// </summary>
        public Message()
        {
            Payload = Array.Empty<byte>();
        }

        /// <summary>
        /// Creates a new message with the specified payload and type.
        /// </summary>
        public Message(byte[] payload, int messageType)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Header = MessageHeader.Create(payload.Length, messageType);
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
                   Payload != null &&
                   Payload.Length == Header.PayloadLength;
        }
    }
}
