using System.Text;
using System.Text.Json;

namespace Selena.Messaging
{
    /// <summary>
    /// Handles serialization and deserialization of messages.
    /// </summary>
    internal static class Serializer
    {
        /// <summary>
        /// Serializes a message to bytes.
        /// </summary>
        public static byte[] SerializeMessage(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!message.IsValid())
            {
                throw new ArgumentException("Invalid message", nameof(message));
            }

            byte[] buffer = new byte[message.GetTotalSize()];

            // Serialize header
            SerializeHeader(message.Header, buffer, 0);

            // Copy payload
            if (message.Payload.Length > 0)
            {
                Buffer.BlockCopy(message.Payload, 0, buffer, MessageHeader.Size, message.Payload.Length);
            }

            return buffer;
        }

        /// <summary>
        /// Deserializes a message from bytes.
        /// </summary>
        public static Message? DeserializeMessage(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < MessageHeader.Size)
            {
                return null;
            }

            // Deserialize header
            MessageHeader header = DeserializeHeader(buffer, offset);
            if (!header.IsValid())
            {
                return null;
            }

            if (header.TotalLength > length)
            {
                return null;
            }

            // Extract payload
            byte[] payload = new byte[header.PayloadLength];
            if (header.PayloadLength > 0)
            {
                Buffer.BlockCopy(buffer, offset + MessageHeader.Size, payload, 0, header.PayloadLength);
            }

            return new Message
            {
                Header = header,
                Payload = payload
            };
        }

        /// <summary>
        /// Creates a message from string data.
        /// </summary>
        public static Message CreateTextMessage(string text, int messageType, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            byte[] payload = encoding.GetBytes(text);
            return new Message(payload, messageType);
        }

        /// <summary>
        /// Gets string data from a message.
        /// </summary>
        public static string GetTextFromMessage(Message message, Encoding? encoding = null)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            encoding ??= Encoding.UTF8;
            return encoding.GetString(message.Payload);
        }

        /// <summary>
        /// Creates a message from an object using JSON serialization.
        /// </summary>
        public static Message CreateJsonMessage<T>(T obj, int messageType)
        {
            string json = JsonSerializer.Serialize(obj);
            return CreateTextMessage(json, messageType);
        }

        /// <summary>
        /// Gets an object from a message using JSON deserialization.
        /// </summary>
        public static T? GetObjectFromMessage<T>(Message message)
        {
            string json = GetTextFromMessage(message);
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Serializes just the header to a byte array.
        /// </summary>
        public static void SerializeHeader(MessageHeader header, byte[] buffer, int offset)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset + MessageHeader.Size > buffer.Length)
            {
                throw new ArgumentException("Buffer too small", nameof(buffer));
            }

            // Use unsafe code for efficient struct serialization
            unsafe
            {
                fixed (byte* pBuffer = &buffer[offset])
                {
                    *(MessageHeader*)pBuffer = header;
                }
            }
        }

        /// <summary>
        /// Deserializes a header from a byte array.
        /// </summary>
        public static MessageHeader DeserializeHeader(byte[] buffer, int offset)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset + MessageHeader.Size > buffer.Length)
            {
                throw new ArgumentException("Buffer too small", nameof(buffer));
            }

            // Use unsafe code for efficient struct deserialization
            unsafe
            {
                fixed (byte* pBuffer = &buffer[offset])
                {
                    return *(MessageHeader*)pBuffer;
                }
            }
        }

        /// <summary>
        /// Peeks at the header without full deserialization.
        /// </summary>
        public static bool TryPeekHeader(byte[] buffer, int offset, int length, out MessageHeader header)
        {
            header = default;

            if (buffer == null || length < MessageHeader.Size)
            {
                return false;
            }

            try
            {
                header = DeserializeHeader(buffer, offset);
                return header.IsValid();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a message to JSON for debugging.
        /// </summary>
        public static string ToJson(Message message, bool includePayloadAsBase64 = false)
        {
            var obj = new
            {
                Header = new
                {
                    message.Header.TotalLength,
                    message.Header.PayloadLength,
                    message.Header.MessageType,
                    Timestamp = new DateTime(message.Header.Timestamp, DateTimeKind.Utc).ToString("O")
                },
                Payload = includePayloadAsBase64 ? Convert.ToBase64String(message.Payload) : null,
                PayloadAsText = TryGetPayloadAsText(message)
            };

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string? TryGetPayloadAsText(Message message)
        {
            try
            {
                // Try to interpret payload as UTF-8 text
                string text = Encoding.UTF8.GetString(message.Payload);

                // Check if it's valid text (no control characters except common ones)
                foreach (char c in text)
                {
                    if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    {
                        return null;
                    }
                }

                return text;
            }
            catch
            {
                return null;
            }
        }
    }
}
