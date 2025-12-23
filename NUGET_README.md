# Selena

**High-Performance Inter-Process Communication for .NET**

[![NuGet Version](https://img.shields.io/nuget/v/Selena)](https://www.nuget.org/packages/Selena/)
[![Downloads](https://img.shields.io/nuget/dt/Selena)](https://www.nuget.org/packages/Selena/)
[![License](https://img.shields.io/github/license/Taiizor/Selena)](https://github.com/Taiizor/Selena/blob/develop/LICENSE)

## Overview

Selena is a **zero-dependency**, high-performance C# library for inter-process communication (IPC) using Memory-Mapped Files. It enables real-time message exchange between processes with minimal latency and maximum throughput.

## Key Features

- **Zero Dependencies** - Pure C# implementation, no external libraries required
- **Cross-Platform** - Works on Windows, Linux, and macOS
- **High Performance** - Up to 600+ MB/s throughput with microsecond latency
- **Thread-Safe** - Built for concurrent multi-threaded scenarios
- **Multiple .NET Versions** - Supports .NET 6/7/8/9/10 and .NET Standard 2.0/2.1
- **Type-Safe** - Generic message serialization with built-in type safety
- **Automatic Recovery** - Handles process crashes and reconnections gracefully

## Installation

```bash
dotnet add package Selena
```

Or via Package Manager Console:

```powershell
Install-Package Selena
```

## Quick Start

```csharp
using Selena.API;

// Create a channel
using var channel = new SelenaChannel("MyChannel");

// Subscribe to messages
channel.MessageReceived += (sender, e) =>
{
    Console.WriteLine($"Received: {e.GetMessageText()}");
};

// Start listening
channel.Start();

// Send a message
await channel.SendMessageAsync("Hello from Selena!");
```

## Advanced Usage

```csharp
// Configure advanced options
var config = new SelenaConfig
{
    ChannelName = "AdvancedChannel",
    BufferSize = 10 * 1024 * 1024,  // 10MB buffer
    OverflowStrategy = OverflowStrategy.Overwrite,
    ScopeMode = ScopeMode.Local,     // No admin required
    PollingInterval = 5,             // 5ms for Linux/macOS
    EnableJsonLogging = true
};

using var channel = new SelenaChannel(config);

// Send typed objects
var order = new Order { Id = 123, Total = 99.99m };
await channel.SendObjectAsync(order, messageType: 1);

// Receive typed objects
channel.MessageReceived += (s, e) =>
{
    if (e.Message.Header.MessageType == 1)
    {
        var receivedOrder = e.GetMessageObject<Order>();
        Console.WriteLine($"Order #{receivedOrder.Id}: ${receivedOrder.Total}");
    }
};
```

## Performance Benchmarks

Benchmarks performed on Intel Core i7-10700K, 32GB RAM, NVMe SSD:

| Message Size | Messages/sec | Throughput | Latency (μs) |
|-------------|--------------|------------|--------------|
| 64 bytes    | 1,200,000+   | 73 MB/s    | < 1          |
| 1 KB        | 680,000+     | 664 MB/s   | 1-2          |
| 4 KB        | 170,000+     | 665 MB/s   | 5-6          |
| 16 KB       | 42,000+      | 656 MB/s   | 20-25        |

## Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `ChannelName` | Unique channel identifier | Required |
| `BufferSize` | Shared memory size | 1 MB |
| `OverflowStrategy` | Buffer full behavior | Overwrite |
| `ScopeMode` | Windows IPC scope | Local |
| `PollingInterval` | Unix polling interval | 10 ms |
| `MaxWaitTime` | Operation timeout | 5000 ms |

## Requirements

- .NET 6.0+ or .NET Standard 2.0/2.1 compatible runtime
- Windows 7+, Linux (kernel 2.6.22+), or macOS 10.12+
- Administrator privileges for Global scope (Windows only)

## Documentation

For full documentation, examples, and API reference, visit the [GitHub repository](https://github.com/Taiizor/Selena).

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/Taiizor/Selena/blob/develop/LICENSE) file for details.