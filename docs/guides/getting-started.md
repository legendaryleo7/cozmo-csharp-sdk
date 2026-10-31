# Getting started
**Version 1.0.0** · Cozmo C# SDK

---

## AI READING INSTRUCTION

Read `[SPEC]` and `[BUG]` blocks for authoritative facts. Read `[NOTE]` blocks for context.

---

## 1. Prerequisites

**[SPEC]**
- .NET SDK 10.0.
- A Cozmo device running the app in SDK mode, or an engine reachable over TCP.
- A supported connection path: TCP, Android with `adb`, or iOS with `usbmuxd`.

**[NOTE]**
The sample projects reference `src/Cozmo/Cozmo.csproj` directly instead of using a NuGet
package.

## 2. Reference the SDK project

**[SPEC]**
Add a project reference to the SDK from your application project. Adjust the relative path
to match your project layout.

```xml
<ItemGroup>
  <ProjectReference Include="../src/Cozmo/Cozmo.csproj" />
</ItemGroup>
```

The application should target `net10.0`.

## 3. Connect and run a command

**[SPEC]**
`CozmoRunner.ConnectAsync` discovers a device, completes the engine handshake, initializes
the primary robot, and waits for it to become ready. Dispose the runner with `await using`.

```csharp
using Anki.Cozmo.Sdk;

await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;

SayText greeting = await robot.SayTextAsync("Hello world");
await greeting.WaitForCompletedAsync(TimeSpan.FromSeconds(15));
```

See [Connecting to Cozmo](connecting.md) for connector selection and timeouts.

## 4. Run a sample

**[SPEC]**
Run the hello-world sample from the repository root:

```bash
dotnet run --project samples/01_hello_world
```

More sample projects are listed in the [README](https://github.com/legendaryleo7/cozmo-csharp-sdk/blob/master/README.md#samples).

## Changelog

- 1.0.0: Initial guide.
