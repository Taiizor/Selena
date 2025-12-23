using Selena.API;

namespace Selena.Examples.Object
{
    /// <summary>
    /// Object Serialization Example
    /// Demonstrates sending and receiving typed objects with Selena.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Selena - Object Example");
            Console.WriteLine("===========================\n");
            Console.WriteLine("Object Serialization Example");
            Console.WriteLine("----------------------------\n");

            SelenaConfig config = new()
            {
                ChannelName = "ObjectChannel",
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
                switch (e.Message.Header.MessageType)
                {
                    case 1: // Person
                        Person? person = e.GetMessageObject<Person>();
                        Console.WriteLine($"Received Person: {person?.Name}, Age: {person?.Age}");
                        break;
                    case 2: // Order
                        Order? order = e.GetMessageObject<Order>();
                        Console.WriteLine($"Received Order: #{order?.OrderId}, Items: {order?.Items.Count}, Total: ${order?.TotalAmount:F2}");
                        break;
                    case 3: // Event
                        SystemEvent? evt = e.GetMessageObject<SystemEvent>();
                        Console.WriteLine($"Received Event: {evt?.EventType} - {evt?.Message} - {evt?.Timestamp}");
                        break;
                }
            };

            // Start listening
            channel.Start();

            // Send different object types
            Person person = new() { Name = "John Doe", Age = 30 };
            await channel.SendObjectAsync(person, messageType: 1);
            Console.WriteLine($"Sent Person: {person.Name}, Age: {person.Age}");

            Order order = new()
            {
                OrderId = 12345,
                Items = ["Item1", "Item2", "Item3"],
                TotalAmount = 99.99m
            };
            await channel.SendObjectAsync(order, messageType: 2);
            Console.WriteLine($"Sent Order: #{order.OrderId}, Items: {order.Items.Count}, Total: ${order.TotalAmount:F2}");

            SystemEvent systemEvent = new()
            {
                EventType = "UserLogin",
                Message = "User logged in successfully",
                Timestamp = DateTime.UtcNow
            };
            await channel.SendObjectAsync(systemEvent, messageType: 3);
            Console.WriteLine($"Sent Event: {systemEvent.EventType} - {systemEvent.Message}");

            // Wait for messages
            await Task.Delay(500);

            Console.WriteLine("\nObject serialization example completed.");
        }
    }

    /// <summary>
    /// Person model for object serialization
    /// </summary>
    public class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    /// <summary>
    /// Order model for object serialization
    /// </summary>
    public class Order
    {
        public int OrderId { get; set; }
        public List<string> Items { get; set; } = [];
        public decimal TotalAmount { get; set; }
    }

    /// <summary>
    /// System event model for object serialization
    /// </summary>
    public class SystemEvent
    {
        public string EventType { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}