using Selena.API;

namespace Selena.Examples.Stress
{
    /// <summary>
    /// Stress Test Example
    /// Demonstrates high-load scenarios with multiple sender threads.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Selena - Stress Test Example");
            Console.WriteLine("================================\n");
            Console.WriteLine("Stress Test");
            Console.WriteLine("-----------\n");

            SelenaConfig config = new()
            {
                ChannelName = "StressChannel",
                BufferSize = 50 * 1024 * 1024, // 50MB
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

            long totalReceived = 0;
            long totalBytes = 0;
            object receivedLock = new();

            // Subscribe to messages
            channel.MessageReceived += (sender, e) =>
            {
                lock (receivedLock)
                {
                    totalReceived++;
                    totalBytes += e.Message.Header.TotalLength;
                }
            };

            // Start listening
            channel.Start();

            CancellationTokenSource cts = new();
            Task[] senderTasks = new Task[5]; // 5 sender threads
            long totalSent = 0;
            object sentLock = new();

            Console.WriteLine("Running stress test with 5 sender threads for 10 seconds...\n");
            DateTime startTime = DateTime.UtcNow;

            // Start sender threads
            for (int i = 0; i < senderTasks.Length; i++)
            {
                int threadId = i;
                senderTasks[i] = Task.Run(async () =>
                {
                    Random random = new();
                    byte[] buffer = new byte[1024];

                    while (!cts.Token.IsCancellationRequested)
                    {
                        random.NextBytes(buffer);
                        if (await channel.SendBytesAsync(buffer, messageType: threadId))
                        {
                            lock (sentLock)
                            {
                                totalSent++;
                            }
                        }
                    }
                });
            }

            // Monitor progress
            Task monitorTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000);

                    long sent, received, bytes;
                    lock (sentLock)
                    {
                        sent = totalSent;
                    }

                    lock (receivedLock)
                    {
                        received = totalReceived;
                        bytes = totalBytes;
                    }

                    double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    Console.WriteLine($"[{elapsed:F0}s] Sent: {sent:N0}, Received: {received:N0}, Throughput: {bytes / elapsed / 1024 / 1024:F2} MB/s");
                }
            });

            // Run for 10 seconds
            await Task.Delay(10000);
            cts.Cancel();

            // Wait for tasks to complete
            await Task.WhenAll(senderTasks);
            await monitorTask;

            // Final statistics
            double totalElapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            Console.WriteLine("\nFinal Statistics:");
            Console.WriteLine($"  Total Sent: {totalSent:N0} messages");
            Console.WriteLine($"  Total Received: {totalReceived:N0} messages");
            Console.WriteLine($"  Total Throughput: {totalBytes / totalElapsed / 1024 / 1024:F2} MB/s");
            Console.WriteLine($"  Average Rate: {totalReceived / totalElapsed:N0} msg/sec");

            Console.WriteLine("\nStress test completed.");
        }
    }
}