# Connecting to Cozmo
**Version 1.0.0** · Cozmo C# SDK

---

## AI READING INSTRUCTION

Read `[SPEC]` and `[BUG]` blocks for authoritative facts. Read `[NOTE]` blocks for context.

---

## 1. Choose a connection path

**[SPEC]**
The default `FirstAvailableConnector` tries enabled connectors in this order:

1. TCP, when a TCP port is configured.
2. Android, through `adb` port forwarding.
3. iOS, through `usbmuxd`.

The Cozmo app must be running in SDK mode. `CozmoRunner.ConnectAsync()` uses this default
connector when no connector is supplied.

## 2. Connect with the default connector

**[SPEC]**
Use `await using` so the runner disposes the robot and connection when the program exits.

```csharp
using Anki.Cozmo.Sdk;

await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;
Console.WriteLine($"Connected to {runner.Device.ConnectorName}");
```

## 3. Configure TCP

**[SPEC]**
- Set `SDK_TCP_PORT` to enable TCP discovery with the default connector.
- Use `new TcpConnector(tcpPort: 5106)` to configure a connector directly.
- `COZMO_PORT` sets the engine bridge port; its default is `5106`.
- The default TCP address is `127.0.0.1`.

```bash
SDK_TCP_PORT=5106 dotnet run --project samples/01_hello_world
```

```csharp
await using var connector = new TcpConnector(tcpPort: 5106);
await using CozmoRunner runner = await CozmoRunner.ConnectAsync(connector: connector);
```

**[NOTE]**
The application’s SDK bridge must be reachable at the configured address and port. TCP is
useful when the app or engine exposes that bridge to the host.

## 4. Configure Android or iOS

**[SPEC]**
- Android requires `adb` and an attached device with USB debugging available. Set
  `ANDROID_ADB_PATH` if `adb` is not on `PATH`.
- iOS connections use `usbmuxd`; the connector uses its default local socket or TCP endpoint.
- The Android connector creates the forwarding rule for `COZMO_PORT` while connecting.

## 5. Set timeouts and cancellation

**[SPEC]**
The default handshake timeout is 10 seconds and the default robot initialization timeout is
30 seconds. Both can be overridden, and a cancellation token can stop discovery or setup.

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

await using CozmoRunner runner = await CozmoRunner.ConnectAsync(
    handshakeTimeout: TimeSpan.FromSeconds(15),
    initializationTimeout: TimeSpan.FromSeconds(45),
    cancellationToken: cancellation.Token);
```

See [Troubleshooting](troubleshooting.md) if discovery or initialization times out.

## Changelog

- 1.0.0: Initial guide.
