using Selena.Messaging;

namespace Selena.Events
{
    /// <summary>
    /// Event arguments for message received events.
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The received message.
        /// </summary>
        public Message Message { get; }

        /// <summary>
        /// The channel name where the message was received.
        /// </summary>
        public string ChannelName { get; }

        /// <summary>
        /// The time when the message was received locally.
        /// </summary>
        public DateTime ReceivedTime { get; }

        /// <summary>
        /// Creates a new instance of MessageReceivedEventArgs.
        /// </summary>
        public MessageReceivedEventArgs(Message message, string channelName)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            ReceivedTime = DateTime.UtcNow;
        }

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
