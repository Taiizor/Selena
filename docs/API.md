# Selena API Documentation

## Core Classes

### SelenaChannel

The main entry point for using Selena.

```csharp
public class SelenaChannel : IDisposable
```

#### Constructors

```csharp
public SelenaChannel(string channelName)
public SelenaChannel(SelenaConfig config)
```

#### Properties

- `Config` - Gets the channel configuration
- `IsStarted` - Gets whether the channel is started and listening

#### Methods

##### Lifecycle Management

```csharp
public void Start()
public void Stop()
public void Dispose()
```

##### Sending Messages

```csharp
public bool SendMessage(string text, int messageType = 0)
public Task<bool> SendMessageAsync(string text, int messageType = 0)
public bool SendBytes(byte[] data, int messageType = 0)
public Task<bool> SendBytesAsync(byte[] data, int messageType = 0)
public bool SendObject<T>(T obj, int messageType = 0)
public Task<bool> SendObjectAsync<T>(T obj, int messageType = 0)
```

##### Receiving Messages

```csharp
public Message? ReceiveMessage(TimeSpan timeout)
public Task<Message?> ReceiveMessageAsync(TimeSpan timeout)
```

##### Events

```csharp
public event EventHandler<MessageReceivedEventArgs>? MessageReceived
```

##### Utilities

```csharp
public ChannelStatistics GetStatistics()
public void ClearBuffer()
public Task<int> BroadcastMessageAsync(string text, int count, int messageType = 0, int delayMs = 10)
```

### SelenaConfig

Configuration for a Selena channel.

```csharp
public class SelenaConfig
```

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ChannelName` | string | Required | Unique identifier for the channel |
| `BufferSize` | int | 1048576 (1MB) | Size of the memory-mapped file buffer in bytes |
| `OverflowStrategy` | OverflowStrategy | Overwrite | Strategy when buffer is full |
| `ScopeMode` | ScopeMode | Local | Windows IPC object scope |
| `PollingInterval` | int | 10 | Polling interval in milliseconds (Unix only) |
| `MaxWaitTime` | int | 5000 | Maximum wait time for blocking operations |
| `EnableJsonLogging` | bool | false | Enable JSON debugging output |

#### Methods

```csharp
public void Validate()
```

### Message

Represents a message for IPC.

```csharp
public class Message
```

#### Properties

- `Header` - Message metadata (MessageHeader struct)
- `Payload` - Message data as byte array

#### Methods

```csharp
public DateTime GetTimestamp()
public int GetTotalSize()
public bool IsValid()
```

### MessageHeader

Message metadata structure.

```csharp
public struct MessageHeader
```

#### Fields

- `TotalLength` - Total message size including header
- `PayloadLength` - Size of payload in bytes
- `MessageType` - User-defined message type identifier
- `Timestamp` - UTC timestamp in ticks
- `Reserved` - Reserved for future use

### MessageReceivedEventArgs

Event arguments for message received events.

```csharp
public class MessageReceivedEventArgs : EventArgs
```

#### Properties

- `Message` - The received message
- `ChannelName` - Name of the channel
- `ReceivedTime` - Local receive timestamp

#### Methods

```csharp
public string GetMessageText()
public T? GetMessageObject<T>()
public TimeSpan GetLatency()
```

### ChannelStatistics

Runtime statistics for a channel.

```csharp
public class ChannelStatistics
```

#### Properties

- `ChannelName` - Name of the channel
- `BufferSize` - Total buffer size
- `AvailableData` - Bytes available to read
- `IsStarted` - Whether channel is active

## Enumerations

### OverflowStrategy

```csharp
public enum OverflowStrategy
{
    Overwrite,  // Overwrite oldest messages
    Block       // Block until space available
}
```

### ScopeMode

```csharp
public enum ScopeMode
{
    Local,  // User session scope (default)
    Global  // System-wide scope (requires admin on Windows)
}
```

## Usage Examples

### Basic Communication

```csharp
// Sender
using var sender = new SelenaChannel("MyChannel");
await sender.SendMessageAsync("Hello!");

// Receiver
using var receiver = new SelenaChannel("MyChannel");
receiver.MessageReceived += (s, e) => 
{
    Console.WriteLine(e.GetMessageText());
};
receiver.Start();
```

### Typed Messages

```csharp
public class MyData
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// Send
var data = new MyData { Id = 1, Name = "Test" };
await channel.SendObjectAsync(data, messageType: 100);

// Receive
channel.MessageReceived += (s, e) =>
{
    if (e.Message.Header.MessageType == 100)
    {
        var data = e.GetMessageObject<MyData>();
        Console.WriteLine($"{data.Id}: {data.Name}");
    }
};
```

### Performance Configuration

```csharp
var config = new SelenaConfig
{
    ChannelName = "HighPerf",
    BufferSize = 50 * 1024 * 1024,  // 50MB
    OverflowStrategy = OverflowStrategy.Overwrite,
    PollingInterval = 1,  // 1ms for minimal latency on Unix
    ScopeMode = ScopeMode.Local
};

using var channel = new SelenaChannel(config);
```

## Thread Safety

- All public methods are thread-safe
- Multiple threads can send messages concurrently
- Message reception is handled on a dedicated thread
- Event handlers are invoked asynchronously

## Error Handling

- Methods return `bool` for success/failure
- `ObjectDisposedException` thrown when using disposed channel
- `ArgumentException` for invalid configuration
- Automatic recovery from transient failures

## Best Practices

1. Always dispose channels when done
2. Use `Local` scope unless cross-session needed
3. Size buffers appropriately for your message volume
4. Use message types to distinguish different payloads
5. Handle `MessageReceived` events quickly
6. Validate received objects before use
