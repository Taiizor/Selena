using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Selena.API;

namespace Selena.Benchmarks
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [SimpleJob(RuntimeMoniker.HostProcess)]
    public class MessageBenchmarks
    {
        private SelenaChannel? _senderChannel;
        private SelenaChannel? _receiverChannel;
        private SelenaChannel? _optimizedSenderChannel;
        private SelenaChannel? _optimizedReceiverChannel;
        private byte[]? _smallData;
        private byte[]? _mediumData;
        private byte[]? _largeData;
        private byte[]? _veryLargeData;
        private readonly string _channelName = $"BenchmarkChannel_{Guid.NewGuid()}";
        private readonly string _optimizedChannelName = $"OptimizedChannel_{Guid.NewGuid()}";

        [GlobalSetup]
        public void Setup()
        {
            // Setup legacy channels
            SelenaConfig config = new()
            {
                ChannelName = _channelName,
                BufferSize = 50 * 1024 * 1024, // 50MB
                ScopeMode = ScopeMode.Local,
                OverflowStrategy = OverflowStrategy.Overwrite,
                EnableOptimizations = false // Use legacy for comparison
            };

            _senderChannel = new SelenaChannel(config);
            _receiverChannel = new SelenaChannel(config);

            _receiverChannel.MessageReceived += (s, e) => { }; // Empty handler
            _receiverChannel.Start();

            // Setup optimized channels
            SelenaConfig optimizedConfig = new()
            {
                ChannelName = _optimizedChannelName,
                BufferSize = 50 * 1024 * 1024, // 50MB
                ScopeMode = ScopeMode.Local,
                OverflowStrategy = OverflowStrategy.Overwrite,
                EnableOptimizations = true,
                EnableCompression = true,
                UseReaderWriterLocks = true,
                CompressionThreshold = 1024
            };

            _optimizedSenderChannel = new SelenaChannel(optimizedConfig);
            _optimizedReceiverChannel = new SelenaChannel(optimizedConfig);

            _optimizedReceiverChannel.MessageReceived += (s, e) => { }; // Empty handler
            _optimizedReceiverChannel.Start();

            // Prepare test data
            _smallData = new byte[64];
            _mediumData = new byte[1024];
            _largeData = new byte[16384];
            _veryLargeData = new byte[1048576]; // 1MB

            Random random = new();
            random.NextBytes(_smallData);
            random.NextBytes(_mediumData);
            random.NextBytes(_largeData);
            random.NextBytes(_veryLargeData);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _senderChannel?.Dispose();
            _receiverChannel?.Dispose();
            _optimizedSenderChannel?.Dispose();
            _optimizedReceiverChannel?.Dispose();
        }

        [Benchmark]
        public async Task SendSmallMessage()
        {
            await _senderChannel!.SendBytesAsync(_smallData!);
        }

        [Benchmark]
        public async Task SendMediumMessage()
        {
            await _senderChannel!.SendBytesAsync(_mediumData!);
        }

        [Benchmark]
        public async Task SendLargeMessage()
        {
            await _senderChannel!.SendBytesAsync(_largeData!);
        }

        [Benchmark]
        public async Task SendTextMessage()
        {
            await _senderChannel!.SendMessageAsync("Hello, Benchmark!");
        }

        [Benchmark]
        public async Task SendObjectMessage()
        {
            BenchmarkData obj = new()
            {
                Id = 123,
                Name = "Benchmark",
                Value = 456.789
            };
            await _senderChannel!.SendObjectAsync(obj);
        }

        [Benchmark]
        public async Task SendLargeMessage_Optimized()
        {
            await _optimizedSenderChannel!.SendBytesAsync(_largeData!);
        }

        [Benchmark]
        public async Task SendVeryLargeMessage()
        {
            await _senderChannel!.SendBytesAsync(_veryLargeData!);
        }

        [Benchmark]
        public async Task SendVeryLargeMessage_Optimized()
        {
            await _optimizedSenderChannel!.SendBytesAsync(_veryLargeData!);
        }

        [Benchmark]
        public async Task SendReceiveRoundTrip()
        {
            TaskCompletionSource<bool> tcs = new();

            void Handler(object? s, Events.MessageReceivedEventArgs e)
            {
                tcs.TrySetResult(true);
                _receiverChannel!.MessageReceived -= Handler;
            }

            _receiverChannel!.MessageReceived += Handler;

            await _senderChannel!.SendMessageAsync("RoundTrip");
            await tcs.Task;
        }

        [Benchmark]
        [Arguments(10)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task SendMultipleMessages(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await _senderChannel!.SendBytesAsync(_smallData!);
            }
        }

        private class BenchmarkData
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public double Value { get; set; }
        }
    }
}
