using FluentAssertions;
using Selena.API;

namespace Selena.Tests
{
    [TestClass]
    public class SelenaChannelTests
    {
        private SelenaChannel? _channel1;
        private SelenaChannel? _channel2;
        private readonly string _testChannelName = $"TestChannel_{Guid.NewGuid()}";

        [TestInitialize]
        public void Setup()
        {
            SelenaConfig config = new()
            {
                ChannelName = _testChannelName,
                BufferSize = 1024 * 1024,
                ScopeMode = ScopeMode.Local,
                OverflowStrategy = OverflowStrategy.Overwrite,
                EnableJsonLogging = false // Disable logging for cleaner test output
            };

            _channel1 = new SelenaChannel(config);
            _channel2 = new SelenaChannel(config);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _channel1?.Dispose();
            _channel2?.Dispose();
        }

        [TestMethod]
        public async Task SendMessage_SimpleText_ShouldBeReceived()
        {
            // Arrange
            string? receivedMessage = null;
            TaskCompletionSource<bool> tcs = new();

            _channel2!.MessageReceived += (sender, e) =>
            {
                receivedMessage = e.GetMessageText();
                tcs.SetResult(true);
            };

            _channel2.Start();
            string testMessage = "Hello, Selena!";

            // Act
            bool sent = await _channel1!.SendMessageAsync(testMessage);
            bool received = await Task.WhenAny(tcs.Task, Task.Delay(1000)) == tcs.Task;

            // Assert
            sent.Should().BeTrue();
            received.Should().BeTrue();
            receivedMessage.Should().Be(testMessage);
        }

        [TestMethod]
        public async Task SendMessage_MultipleMessages_ShouldBeReceivedInOrder()
        {
            // Arrange
            List<string> receivedMessages = [];
            List<string> expectedMessages = Enumerable.Range(1, 10).Select(i => $"Message {i}").ToList();
            TaskCompletionSource<bool> tcs = new();

            _channel2!.MessageReceived += (sender, e) =>
            {
                receivedMessages.Add(e.GetMessageText());
                if (receivedMessages.Count == expectedMessages.Count)
                {
                    tcs.SetResult(true);
                }
            };

            _channel2.Start();

            // Act
            foreach (string message in expectedMessages)
            {
                await _channel1!.SendMessageAsync(message);
            }

            bool received = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task;

            // Assert
            received.Should().BeTrue();
            receivedMessages.Should().BeEquivalentTo(expectedMessages, options => options.WithStrictOrdering());
        }

        [TestMethod]
        public async Task SendObject_ComplexType_ShouldSerializeCorrectly()
        {
            // Arrange
            TestData? receivedData = null;
            TaskCompletionSource<bool> tcs = new();

            TestData testData = new()
            {
                Id = 42,
                Name = "Test Object",
                Timestamp = DateTime.UtcNow,
                Values = [1, 2, 3, 4, 5]
            };

            _channel2!.MessageReceived += (sender, e) =>
            {
                receivedData = e.GetMessageObject<TestData>();
                tcs.SetResult(true);
            };

            _channel2.Start();

            // Act
            bool sent = await _channel1!.SendObjectAsync(testData);
            bool received = await Task.WhenAny(tcs.Task, Task.Delay(1000)) == tcs.Task;

            // Assert
            sent.Should().BeTrue();
            received.Should().BeTrue();
            receivedData.Should().BeEquivalentTo(testData);
        }

        [TestMethod]
        public void ChannelStatistics_AfterSending_ShouldReflectCorrectState()
        {
            // Arrange
            _channel1!.Start();

            // Act
            ChannelStatistics stats = _channel1.GetStatistics();

            // Assert
            stats.Should().NotBeNull();
            stats.ChannelName.Should().Be(_testChannelName);
            stats.BufferSize.Should().Be(1024 * 1024);
            stats.IsStarted.Should().BeTrue();
        }

        [TestMethod]
        public async Task SendBytes_LargeData_ShouldHandleCorrectly()
        {
            // Arrange
            byte[]? receivedData = null;
            TaskCompletionSource<bool> tcs = new();
            byte[] testData = new byte[10000];
            new Random().NextBytes(testData);

            _channel2!.MessageReceived += (sender, e) =>
            {
                receivedData = e.Message.Payload;
                tcs.SetResult(true);
            };

            _channel2.Start();

            // Act
            bool sent = await _channel1!.SendBytesAsync(testData);
            bool received = await Task.WhenAny(tcs.Task, Task.Delay(1000)) == tcs.Task;

            // Assert
            sent.Should().BeTrue();
            received.Should().BeTrue();
            receivedData.Should().BeEquivalentTo(testData);
        }

        [TestMethod]
        public void Start_WhenAlreadyStarted_ShouldNotThrow()
        {
            // Arrange
            _channel1!.Start();

            // Act
            Action act = _channel1.Start;

            // Assert
            act.Should().NotThrow();
            _channel1.IsStarted.Should().BeTrue();
        }

        [TestMethod]
        public void Stop_WhenNotStarted_ShouldNotThrow()
        {
            // Act
            Action act = () => _channel1!.Stop();

            // Assert
            act.Should().NotThrow();
            _channel1!.IsStarted.Should().BeFalse();
        }

        [TestMethod]
        public void Dispose_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            SelenaChannel channel = new(_testChannelName + "_dispose");

            // Act
            Action act = () =>
            {
                channel.Dispose();
                channel.Dispose();
            };

            // Assert
            act.Should().NotThrow();
        }

        [TestMethod]
        public void SendMessage_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _channel1!.Dispose();

            // Act
            Func<Task> act = async () => await _channel1.SendMessageAsync("test");

            // Assert
            act.Should().ThrowAsync<ObjectDisposedException>();
        }

        [TestMethod]
        public async Task BidirectionalChat_BothChannelsSendAndReceive_AllMessagesShouldBeReceived()
        {
            // Arrange
            List<string> channel1Received = [];
            List<string> channel2Received = [];
            object lock1 = new();
            object lock2 = new();

            List<string> channel1Sent = ["A1", "A2", "A3", "A4", "A5"];
            List<string> channel2Sent = ["B1", "B2", "B3", "B4", "B5"];

            TaskCompletionSource<bool> tcs1 = new();
            TaskCompletionSource<bool> tcs2 = new();

            _channel1!.MessageReceived += (sender, e) =>
            {
                string text = e.GetMessageText();
                // Channel 1 should receive messages from Channel 2 (B* messages)
                if (text.StartsWith("B"))
                {
                    lock (lock1)
                    {
                        channel1Received.Add(text);
                        if (channel1Received.Count == channel2Sent.Count)
                        {
                            tcs1.TrySetResult(true);
                        }
                    }
                }
            };

            _channel2!.MessageReceived += (sender, e) =>
            {
                string text = e.GetMessageText();
                // Channel 2 should receive messages from Channel 1 (A* messages)
                if (text.StartsWith("A"))
                {
                    lock (lock2)
                    {
                        channel2Received.Add(text);
                        if (channel2Received.Count == channel1Sent.Count)
                        {
                            tcs2.TrySetResult(true);
                        }
                    }
                }
            };

            // Start both channels listening
            _channel1.Start();
            _channel2.Start();

            // Give receivers time to initialize
            await Task.Delay(100);

            // Act - Both channels send messages interleaved
            for (int i = 0; i < 5; i++)
            {
                await _channel1.SendMessageAsync(channel1Sent[i]);
                await Task.Delay(10);
                await _channel2.SendMessageAsync(channel2Sent[i]);
                await Task.Delay(10);
            }

            // Wait for messages to be received
            bool received1 = await Task.WhenAny(tcs1.Task, Task.Delay(5000)) == tcs1.Task;
            bool received2 = await Task.WhenAny(tcs2.Task, Task.Delay(5000)) == tcs2.Task;

            // Assert
            received1.Should().BeTrue($"Channel 1 should have received all B* messages. Got: {string.Join(",", channel1Received)}");
            received2.Should().BeTrue($"Channel 2 should have received all A* messages. Got: {string.Join(",", channel2Received)}");

            channel1Received.Should().HaveCount(5, "Channel 1 should receive 5 messages from Channel 2");
            channel2Received.Should().HaveCount(5, "Channel 2 should receive 5 messages from Channel 1");

            channel1Received.Should().BeEquivalentTo(channel2Sent, "Channel 1 should receive all B* messages");
            channel2Received.Should().BeEquivalentTo(channel1Sent, "Channel 2 should receive all A* messages");
        }

        [TestMethod]
        public async Task BidirectionalChat_RapidMessages_NoMessageLoss()
        {
            // Arrange - This test simulates the actual chat scenario from the bug report
            List<string> channel1Received = [];
            List<string> channel2Received = [];
            object lock1 = new();
            object lock2 = new();

            int messageCount = 20;
            List<string> channel1Sent = Enumerable.Range(1, messageCount).Select(i => $"User1_Msg{i}").ToList();
            List<string> channel2Sent = Enumerable.Range(1, messageCount).Select(i => $"User2_Msg{i}").ToList();

            TaskCompletionSource<bool> tcs1 = new();
            TaskCompletionSource<bool> tcs2 = new();

            _channel1!.MessageReceived += (sender, e) =>
            {
                string text = e.GetMessageText();
                if (text.StartsWith("User2_"))
                {
                    lock (lock1)
                    {
                        channel1Received.Add(text);
                        if (channel1Received.Count == channel2Sent.Count)
                        {
                            tcs1.TrySetResult(true);
                        }
                    }
                }
            };

            _channel2!.MessageReceived += (sender, e) =>
            {
                string text = e.GetMessageText();
                if (text.StartsWith("User1_"))
                {
                    lock (lock2)
                    {
                        channel2Received.Add(text);
                        if (channel2Received.Count == channel1Sent.Count)
                        {
                            tcs2.TrySetResult(true);
                        }
                    }
                }
            };

            // Start both channels listening
            _channel1.Start();
            _channel2.Start();

            // Give receivers time to initialize
            await Task.Delay(200);

            // Act - Simulate typing from both users (sequential, not parallel, for reliability)
            for (int i = 0; i < messageCount; i++)
            {
                await _channel1.SendMessageAsync(channel1Sent[i]);
                await _channel2.SendMessageAsync(channel2Sent[i]);
                await Task.Delay(5); // Small delay between messages
            }

            // Wait for messages to be received
            bool received1 = await Task.WhenAny(tcs1.Task, Task.Delay(10000)) == tcs1.Task;
            bool received2 = await Task.WhenAny(tcs2.Task, Task.Delay(10000)) == tcs2.Task;

            // Assert
            received1.Should().BeTrue($"Channel 1 should have received all User2 messages. Got {channel1Received.Count}: {string.Join(",", channel1Received)}");
            received2.Should().BeTrue($"Channel 2 should have received all User1 messages. Got {channel2Received.Count}: {string.Join(",", channel2Received)}");

            channel1Received.Should().HaveCount(messageCount, $"Channel 1 should receive {messageCount} messages from Channel 2");
            channel2Received.Should().HaveCount(messageCount, $"Channel 2 should receive {messageCount} messages from Channel 1");
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public DateTime Timestamp { get; set; }
            public List<int> Values { get; set; } = [];
        }
    }
}
