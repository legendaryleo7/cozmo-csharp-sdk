# Troubleshooting
**Version 1.0.0** · Cozmo C# SDK

---

## AI READING INSTRUCTION

Read `[SPEC]` and `[BUG]` blocks for authoritative facts. Read `[NOTE]` blocks for context.

---

## 1. No device is found

**[SPEC]**
Check the connection path that the default connector can use:

| Connection | Checks |
|---|---|
| TCP | Set `SDK_TCP_PORT`, or pass `tcpPort` to `TcpConnector`. Confirm the app’s bridge is reachable at that address and port. |
| Android | Confirm `adb` is installed and the device is attached. Run `adb devices`; set `ANDROID_ADB_PATH` if the executable is not on `PATH`. |
| iOS | Confirm the device is attached and `usbmuxd` is available at its local socket or TCP endpoint. |

The app must be in SDK mode. The default connector skips TCP unless a port is configured,
then tries Android and iOS.

## 2. Connection or initialization times out

**[SPEC]**
`CozmoRunner.ConnectAsync` defaults to a 10-second handshake timeout and a 30-second
initialization timeout. Check that the SDK app is active, the selected connector reaches the
engine, and the engine port matches `COZMO_PORT` (default `5106`). Increase the relevant
timeout if setup is expected to take longer.

## 3. SDK version mismatch

**[SPEC]**
`SdkVersionMismatchException` means the engine reported a protocol version that the library
does not accept. Use a Cozmo app version compatible with this SDK’s protocol definitions.

**[NOTE]**
This repository ports Anki’s Cozmo Python SDK 1.4.12 and regenerates CLAD from cozmoclad
3.6.6. See the [CLAD audit](https://github.com/legendaryleo7/cozmo-csharp-sdk/blob/master/docs/clad-audit.md)
for the compatibility background.

## 4. A cube or face is never observed

**[SPEC]**
World wait methods time out only when a timeout is supplied. Check that the object or face is
within Cozmo’s view and wait with an explicit timeout while the app is active.

```csharp
try
{
    LightCube cube = await robot.World.WaitForObservedLightCubeAsync(
        TimeSpan.FromSeconds(30));
}
catch (TimeoutException)
{
    Console.WriteLine("No cube was observed before the timeout.");
}
```

## 5. Camera frames do not arrive

**[SPEC]**
Enable streaming with `SetImageStreamEnabledAsync(true)` before waiting for
`EvtNewRawCameraImage`. Use an explicit timeout and disable the stream in `finally` when the
capture ends.

See [Camera frames](camera.md) for a complete example.

## Changelog

- 1.0.0: Initial guide.
