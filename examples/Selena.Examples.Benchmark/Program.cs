using Selena.API;
using System.Diagnostics;

namespace Selena.Examples.Benchmark
{
    /// <summary>
    /// Performance Benchmark Example
    /// Demonstrates throughput testing with different message sizes.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Selena - Benchmark Example");
            Console.WriteLine("==============================\n");
            Console.WriteLine("Performance Benchmark");
            Console.WriteLine("---------------------\n");

            SelenaConfig config = new()
            {
                ChannelName = "BenchmarkChannel",
                BufferSize = 10 * 1024 * 1024, // 10MB
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

            int receivedCount = 0;
            object receivedLock = new();

            // Subscribe to messages
            channel.MessageReceived += (sender, e) =>
            {
                lock (receivedLock)
                {
                    receivedCount++;
                }
            };

            // Start listening
            channel.Start();

            // Benchmark parameters
            int[] messageSizes = [64, 256, 1024, 4096, 16384];
            int messageCount = 10000;

            foreach (int size in messageSizes)
            {
                Console.WriteLine($"\nTesting with {size} byte messages...");

                receivedCount = 0;
                byte[] data = new byte[size];
                new Random().NextBytes(data);

                Stopwatch sw = Stopwatch.StartNew();

                // Send messages
                for (int i = 0; i < messageCount; i++)
                {
                    channel.SendBytes(data, messageType: size);
                }

                sw.Stop();

                // Wait for messages to be received
                await Task.Delay(1000);

                double sendRate = messageCount / sw.Elapsed.TotalSeconds;
                double throughputMBps = messageCount * size / (sw.Elapsed.TotalSeconds * 1024 * 1024);

                Console.WriteLine($"  Sent: {messageCount:N0} messages in {sw.Elapsed.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Rate: {sendRate:N0} msg/sec");
                Console.WriteLine($"  Throughput: {throughputMBps:F2} MB/s");
                Console.WriteLine($"  Received: {receivedCount:N0} messages");
            }

            Console.WriteLine("\nBenchmark completed.");
        }
    }
}