using Selena.API;

namespace Selena.Examples.Chat
{
    /// <summary>
    /// Chat Application Example
    /// Demonstrates a multi-process chat application using Selena.
    /// Run multiple instances with different usernames to test communication.
    /// Usage: dotnet run -- [username]
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            string userName = args.Length > 0 ? args[0] : "User";

            Console.WriteLine("Selena - Chat Example");
            Console.WriteLine("=========================\n");
            Console.WriteLine($"Chat Example - User: {userName}");
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Type messages to send. Type 'exit' to quit.\n");

            const int ChatMessageType = 100;
            object consoleLock = new();

            SelenaConfig config = new()
            {
                ChannelName = "ChatChannel",
                BufferSize = 1 * 1024 * 1024, // 1MB
                OverflowStrategy = OverflowStrategy.Overwrite,
                PollingInterval = 5,
                ScopeMode = ScopeMode.Local,
                CompressionThreshold = 1024,
                MaxWaitTime = 5000,
                UseReaderWriterLocks = false,
                EnableOptimizations = false,
                EnableCompression = false,
                EnableJsonLogging = false
            };

            using SelenaChannel channel = new(config);

            // Subscribe to messages
            channel.MessageReceived += (sender, e) =>
            {
                if (e.Message.Header.MessageType != ChatMessageType)
                {
                    return;
                }

                ChatMessage? chatMessage = e.GetMessageObject<ChatMessage>();
                if (chatMessage == null)
                {
                    return;
                }

                if (string.Equals(chatMessage.Sender, userName, StringComparison.OrdinalIgnoreCase))
                {
                    // Local echo is handled immediately after sending
                    return;
                }

                lock (consoleLock)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[{chatMessage.Timestamp:HH:mm:ss}] {chatMessage.Sender}: {chatMessage.Text}");
                    Console.Write("> ");
                }
            };

            // Start listening
            channel.Start();

            // Input loop
            while (true)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
                {
                    break;
                }

                ChatMessage chatMessage = new()
                {
                    Sender = userName,
                    Text = input,
                    Timestamp = DateTime.Now
                };

                await channel.SendObjectAsync(chatMessage, messageType: ChatMessageType);

                lock (consoleLock)
                {
                    Console.WriteLine($"[{chatMessage.Timestamp:HH:mm:ss}] {chatMessage.Sender} (you): {chatMessage.Text}");
                }
            }

            Console.WriteLine("Chat ended.");
        }
    }

    /// <summary>
    /// Chat message model for serialization
    /// </summary>
    public class ChatMessage
    {
        public string Sender { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}