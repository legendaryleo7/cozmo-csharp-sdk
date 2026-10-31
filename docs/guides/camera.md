# Camera frames
**Version 1.0.0** · Cozmo C# SDK

---

## AI READING INSTRUCTION

Read `[SPEC]` and `[BUG]` blocks for authoritative facts. Read `[NOTE]` blocks for context.

---

## 1. Enable the image stream

**[SPEC]**
Call `robot.Camera.SetImageStreamEnabledAsync(true)` to request continuous frames. Frames
arrive as `EvtNewRawCameraImage`; each event’s `Image.RawImage` is JPEG data in a `byte[]`.

The SDK does not decode or annotate the image bytes and does not bundle an image codec.

## 2. Save frames to disk

**[SPEC]**
Disable streaming in a `finally` block so the stream is stopped if saving a frame fails.

```csharp
using Anki.Cozmo.Sdk;

await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;

await robot.Camera.SetImageStreamEnabledAsync(true);
try
{
    EvtNewRawCameraImage frame = await robot.Camera.WaitFor<EvtNewRawCameraImage>(
        timeout: TimeSpan.FromSeconds(10));

    await File.WriteAllBytesAsync("frame.jpg", frame.Image.RawImage);
}
finally
{
    await robot.Camera.SetImageStreamEnabledAsync(false);
}
```

The event wait has a timeout so the program can handle a camera that does not send a frame.

## 3. Choose color or grayscale images

**[SPEC]**
Call `SetColorImageEnabledAsync(true)` to request color images or pass `false` for grayscale.
The default is grayscale.

## 4. Run the complete sample

**[SPEC]**
The repository camera sample saves five frames under a `frames` directory and then disables
the stream:

```bash
dotnet run --project samples/04_camera_frames
```

## Changelog

- 1.0.0: Initial guide.
