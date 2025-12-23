using Selena.API;

namespace Selena.Examples.Simple
{
    /// <summary>
    /// Simple Send/Receive Example
    /// Demonstrates basic message sending and receiving with Selena.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Selena - Simple Example");
            Console.WriteLine("===========================\n");
            Console.WriteLine("Simple Send/Receive Example");
            Console.WriteLine("---------------------------\n");

            // Configure channel
            SelenaConfig config = new()
            {
                ChannelName = "SimpleChannel",
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

            // Create channel
            using SelenaChannel channel = new(config);

            // Subscribe to messages
            channel.MessageReceived += (sender, e) =>
            {
                Console.WriteLine($"Received: {e.GetMessageText()} (Type: {e.Message.Header.MessageType})");
            };

            // Start listening
            channel.Start();

            // Send some messages
            for (int i = 1; i <= 5; i++)
            {
                string message = $"Hello from Selena #{i}";
                bool sent = await channel.SendMessageAsync(message, messageType: i);
                Console.WriteLine($"Sent: {message} - Success: {sent}");
                await Task.Delay(100);
            }

            // Wait a bit for messages
            await Task.Delay(500);

            // Get statistics
            ChannelStatistics stats = channel.GetStatistics();
            Console.WriteLine($"\nChannel Statistics: {stats}");
        }
    }
}