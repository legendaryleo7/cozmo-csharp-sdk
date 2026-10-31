# World and light cubes
**Version 1.0.0** · Cozmo C# SDK

---

## AI READING INSTRUCTION

Read `[SPEC]` and `[BUG]` blocks for authoritative facts. Read `[NOTE]` blocks for context.

---

## 1. Wait for a visible cube

**[SPEC]**
The robot’s `World` tracks objects reported by the engine. Use
`WaitForObservedLightCubeAsync` to wait for a cube; the method returns an already-visible cube
by default and throws `TimeoutException` if the supplied timeout expires.

```csharp
LightCube cube = await robot.World.WaitForObservedLightCubeAsync(
    TimeSpan.FromSeconds(30));

Console.WriteLine($"Observed {cube.CubeId.Name}");
```

The default `null` timeout waits indefinitely. Set a timeout when the application needs to
recover if no cube is observed.

## 2. Change the cube lights

**[SPEC]**
Set all four LEDs to the same light with `SetLightsAsync`, or pass four lights to
`SetLightCornersAsync` to set each slot separately.

```csharp
await cube.SetLightsAsync(Lights.Green);
await Task.Delay(TimeSpan.FromSeconds(2));

await cube.SetLightCornersAsync(
    Lights.Red,
    Lights.Green,
    Lights.Blue,
    Lights.White);

await Task.Delay(TimeSpan.FromSeconds(2));
await cube.SetLightsOffAsync();
```

The API uses the CLAD corner order; it does not define a physical orientation for the four
slots.

## 3. Wait for cube events

**[SPEC]**
Use the cube’s typed wait methods for interactions such as taps.

```csharp
EvtObjectTapped tap = await cube.WaitForTapAsync(TimeSpan.FromSeconds(30));
Console.WriteLine($"Cube tap count: {tap.TapCount}; intensity: {tap.TapIntensity}");
```

## 4. Run the sample

**[SPEC]**
The repository’s cube sample waits for an observed cube, animates its LEDs, then turns them
off:

```bash
dotnet run --project samples/02_cube_lights
```

## Changelog

- 1.0.0: Initial guide.
