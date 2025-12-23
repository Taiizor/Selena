# Selena AI Guide

## Architecture Snapshot
- `SelenaChannel` (`src/Selena/API/SelenaChannel.cs`) is the public facade that wires `MMFManager`, the `CircularBuffer`, sync primitives, and either `Sender`/`Receiver` or `OptimizedSender`/`OptimizedSync` depending on `SelenaConfig.EnableOptimizations` + `UseReaderWriterLocks`.
- Core memory flow: `Sender` serializes `Message` structs via `Messsaging/Serializer.cs`, writes header+payload into the memory-mapped `CircularBuffer`, and signals sync primitives; `Receiver` peeks headers, drains complete messages, then dispatches through `Events/EventDispatcher` to user handlers.
- Two synchronization flavors matter: `CrossPlatformSync` (mutex + event handles on Windows, timer polling elsewhere) and `OptimizedSync` (reader/writer locks + `System.Threading.Channels` notifications). Touching buffer logic usually requires touching *both* paths.
- Performance helpers live in `Core/BufferPoolManager.cs` (ArrayPool tiers) and `Core/CompressionManager.cs` (gzip + message flags). Always clean up `PooledBuffer` via `using` to prevent leaks.

## Configuration & Protocol Contracts
- `SelenaConfig` enforces validated defaults (1 MB buffer, overwrite overflow, 10 ms polling). Keep channel names unique per process pair and note `ScopeMode.Global` requires admin rights on Windows.
- Message layout is fixed-width `MessageHeader` (24 bytes) plus payload; compressed messages append a 4-byte original size and a flag byte (see `CompressedMessageHeader`). When changing headers, mirror updates in `Serializer`, `Receiver`, and any tooling that parses `MessageHeader`.
- `OverflowStrategy.Block` vs `Overwrite` changes how `CircularBuffer.Write` retries; tests expect block mode to respect `MaxWaitTime`. When increasing payload size limits ensure `_dataSize` (> header) invariants stay satisfied.

## Build, Test, and Bench
- Multi-targeting (`net6.0` through `net10.0` plus `netstandard`) with `<TreatWarningsAsErrors>true>` means fixes must satisfy older TFMs; prefer BCL APIs available across all targets.
- Fast local loop:
  ```pwsh
  dotnet build Selena.sln
  dotnet test tests/Selena.Tests/Selena.Tests.csproj
  ```
- CI-equivalent script toggles live in `build.ps1`; e.g. `./build.ps1 -Configuration Release -Test -Pack` runs build, tests with coverage to `artifacts/TestResults`, and packs NuGet output under `artifacts/packages`.
- Microbenchmarks: `dotnet run -c Release --project tests/Selena.Benchmarks` (BenchmarkDotNet, net10.0). Examples/stress repros: `dotnet run --project examples/Selena.Examples -- stress`.

## Testing & Diagnostics
- Unit tests (`tests/Selena.Tests`) use MSTest + FluentAssertions. Always generate per-test channel names (`Guid` suffix) to avoid cross-test contention; follow the existing pattern in `SelenaChannelTests`.
- Receiver-side bugs often need repros with logging: flip `SelenaConfig.EnableJsonLogging` and `ScopeMode.Local` to capture deterministic traces before filing tests.
- `artifacts/bin/Selena/...` holds per-TFM outputs plus XML docs; keep public API changes documented in `docs/API.md` or `CHANGELOG.md` to avoid shipping mismatches.

## Contribution Patterns
- Favor async send helpers when working with optimized mode—`OptimizedSender` builds on the same serializer but insists on pooled buffers, so ensure awaiters are preserved.
- When editing memory layout or synchronization, add targeted stress coverage under `examples/Selena.Examples` (e.g., extend `RunStressTest`) and, when possible, a regression in `tests/Selena.Tests` to keep both legacy and optimized paths honest.
- Maintain cross-platform parity: Windows paths rely on named kernel objects (`Global\Channel_Mutex`), Unix paths rely on polling; any new signaling must either be abstracted in `CrossPlatformSync`/`OptimizedSync` or guarded by runtime checks.