# Cozmo C# SDK

A C# port of the Anki Cozmo Python SDK **1.4.12**, targeting **.NET 10**, with a
regenerated CLAD layer built from **cozmoclad 3.6.6**.

The original C# drop in this repository could not talk to current Cozmo apps: its
`CladCSharp.dll` is a 32-bit .NET Framework assembly and its CLAD schema is from 2018
(see [`docs/clad-audit.md`](docs/clad-audit.md)). This repository now ships a supported
library generated from the Python SDK's own CLAD definitions and verified byte-for-byte
against the Python implementation.

## Requirements

- .NET SDK 10.0
- The Cozmo app in SDK mode on an Android or iOS device (or an engine reachable over TCP)
- Python 3.10+ only if you want to regenerate the CLAD layer (see
  [`tools/cladgen/README.md`](tools/cladgen/README.md))

## Build and test

```bash
dotnet build Cozmo.sln -warnaserror
dotnet test Cozmo.sln
```

The suite currently runs **791 tests**, including 410 byte-exact CLAD oracle cases
generated from the Python layer, and covers the dispatcher, connection/handshake,
actions, world/objects/faces/pets, camera, navigation map, animation, behavior,
audio, lights, OLED packing, device connectors, and the runner.

## Quick start

```csharp
using Anki.Cozmo.Sdk;

await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;

SayText hello = await robot.SayTextAsync("Hello world");
await hello.WaitForCompletedAsync(TimeSpan.FromSeconds(15));

DriveStraight drive = await robot.DriveStraightAsync(
    Distance.FromMillimeters(50),
    Speed.FromMillimetersPerSecond(50));
await drive.WaitForCompletedAsync(TimeSpan.FromSeconds(15));
```

`CozmoRunner.ConnectAsync` picks the first available device, performs the engine
handshake, creates the `Robot`, and waits for `EvtRobotReady`. Disposing the runner
disposes the robot, the connection, and (when it created it) the connector.

## Connecting

The default connector (`FirstAvailableConnector`) tries, in order:

1. **TCP** — only when a port is configured. Set `SDK_TCP_PORT` (for example
   `SDK_TCP_PORT=5106`) or construct `new TcpConnector(tcpPort: 5106)`.
2. **Android** — finds `adb` from `ANDROID_ADB_PATH`, the constructor argument, `PATH`,
   or the usual Android SDK locations, runs `adb forward tcp:5106 tcp:5106` per
   device, and connects to `127.0.0.1:5106`.
3. **iOS** — talks to `usbmuxd` (`/var/run/usbmuxd`, or `127.0.0.1:27015` on Windows).

`COZMO_PORT` overrides the engine port (default `5106`). You can pass any
`IDeviceConnector` to `CozmoRunner.ConnectAsync` to control discovery.

## Samples

| Sample | Shows |
|---|---|
| [`samples/01_hello_world`](samples/01_hello_world) | connect, say text, drive straight, turn |
| [`samples/02_cube_lights`](samples/02_cube_lights) | wait for a light cube and animate its LEDs |
| [`samples/03_events`](samples/03_events) | subscribe to robot state and world events |
| [`samples/04_camera_frames`](samples/04_camera_frames) | stream JPEG frames to disk |

```bash
dotnet run --project samples/01_hello_world
```

## API reference

The public API reference is generated with DocFX and published at
<https://legendaryleo7.github.io/cozmo-csharp-sdk/>. To build it locally, run:

```bash
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
```

The generated site is written to `docs/_site`.

## Repository layout

| Path | Contents |
|---|---|
| `src/Cozmo/Base` | event dispatcher, event records, handlers/filters, exceptions, `CladEnum` |
| `src/Cozmo/Clad/Generated` | 739 generated CLAD types and the CLAD identity/hashes |
| `src/Cozmo/Generated/SdkEnums.g.cs` | named `CladEnum` catalogues (`ActionResults`, `Triggers`, `AudioEvents`, …) |
| `src/Cozmo/Connection` | `CozmoConnection`, handshake, message dispatch |
| `src/Cozmo/Actions` | `CozmoAction`, `ActionDispatcher`, `ActionResults` |
| `src/Cozmo/Anim`, `Behavior`, `Audio`, `Display` | animation, behavior, songs/audio and OLED helpers |
| `src/Cozmo/Objects`, `Faces`, `Pets` | world entities and their events |
| `src/Cozmo/Camera`, `Nav` | camera stream/chunks and the navigation memory map |
| `src/Cozmo/World`, `Robot` | `World`, `Robot`, robot actions and initialization |
| `src/Cozmo/Lights`, `Util` | colours/lights, `Pose`, `Angle`, `Distance`, `Speed`, vectors, `TimeoutHelper` |
| `src/Cozmo/Run` | device connectors, stream transport, `CozmoRunner` |
| `tests/Cozmo.Tests` | unit tests plus the generated CLAD oracle |
| `tools/cladgen`, `tools/utilgen`, `tools/camnavgen` | schema/CLAD/SDK-enum generators and golden-data generators |
| `legacy` | the original unsupported Xamarin interface and DLL, kept for reference |

## Compatibility notes (differences from the Python SDK)

- **Async everywhere.** There is no synchronous bridge or private event loop; use
  `async`/`await`. `DisplayOledFaceImageAsync`, `WaitForCompletedAsync`,
  `WaitForObservedLightCubeAsync`, etc. follow the Python semantics, with `null`
  timeouts meaning "wait forever" unless documented otherwise.
- **Events are immutable records.** Register with `AddHandler<TEvent>` or typed helpers
  (`action.OnCompleted`, `cube.WaitForTapAsync`), and wait with
  `WaitFor<TEvent>(predicate, timeout)`.
- **No imaging stack.** The SDK does not depend on PIL, NumPy, or an image codec.
  Camera events carry JPEG `byte[]` (`CameraImage`), `OledFace` consumes already
  packed or grayscale bytes, and `CameraImage` has no `annotate` support.
- **No bundled viewers or logging setup.** The Tk/OpenGL viewers and
  `setup_basic_logging` are not ported; pass an `ILoggerFactory`/`ILogger<T>` from
  your host.
- **Small API type adaptations:** `Color` is a value type, Python's `Timeout` helper
  is `TimeoutHelper` (so `System.Threading.Timeout` stays unambiguous), `SongNote` is
  a value type with `ToClad()`, and `Behavior.StopAsync` is asynchronous.
- **`perform_off_charger`** is `await using var context = await robot.PerformOffChargerAsync();`.
- **One primary robot.** The runner creates robot id `1`; per-robot message routing for
  multiple robots is not implemented (the Python SDK also only exposes the primary robot).
- **Animation list is requested during initialization** and must load before
  `EvtRobotReady`, matching the Python connection's startup sequence.

## Regenerating the CLAD layer

The generated code is committed. To regenerate it from a cozmoclad wheel and re-run the
Python oracle, follow [`tools/cladgen/README.md`](tools/cladgen/README.md). The oracle
test packs the same values in Python and asserts that the C# layer produces identical
bytes, so wire-format drift fails the build.

## Legacy interface

`legacy/` contains the original `CladCSharp.dll` and Xamarin sample project. It is
unsupported, not built, and cannot interoperate with current Cozmo apps; see
[`legacy/README.md`](legacy/README.md).

## License

Apache License 2.0. See [`LICENSE.txt`](LICENSE.txt).
