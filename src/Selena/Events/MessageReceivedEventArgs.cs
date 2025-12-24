using Selena.Messaging;

namespace Selena.Events
{
    /// <summary>
    /// Event arguments for message received events.
    /// </summary>
    /// <remarks>
    /// Creates a new instance of MessageReceivedEventArgs.
    /// </remarks>
    public class MessageReceivedEventArgs(Message message, string channelName) : EventArgs
    {
        /// <summary>
        /// The received message.
        /// </summary>
        public Message Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

        /// <summary>
        /// The channel name where the message was received.
        /// </summary>
        public string ChannelName { get; } = channelName ?? throw new ArgumentNullException(nameof(channelName));

        /// <summary>
        /// The time when the message was received locally.
        /// </summary>
        public DateTime ReceivedTime { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the message payload as text.
        /// </summary>
        public string GetMessageText()
        {
            return Serializer.GetTextFromMessage(Message);
        }

        /// <summary>
        /// Gets the message payload as a deserialized object.
        /// </summary>
        public T? GetMessageObject<T>()
        {
            return Serializer.GetObjectFromMessage<T>(Message);
        }

        /// <summary>
        /// Gets the time elapsed since the message was sent.
        /// </summary>
        public TimeSpan GetLatency()
        {
            return ReceivedTime - Message.GetTimestamp();
        }
    }
}
