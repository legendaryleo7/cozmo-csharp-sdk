# Actions and events
**Version 1.0.0** · Cozmo C# SDK

---

## AI READING INSTRUCTION

Read `[SPEC]` and `[BUG]` blocks for authoritative facts. Read `[NOTE]` blocks for context.

---

## 1. Run an action

**[SPEC]**
Robot commands such as `DriveStraightAsync` return an action object. Await the command to
send it, then await `WaitForCompletedAsync` to wait for the robot’s completion event.

```csharp
DriveStraight drive = await robot.DriveStraightAsync(
    Distance.FromMillimeters(50),
    Speed.FromMillimetersPerSecond(50));

await drive.WaitForCompletedAsync(TimeSpan.FromSeconds(15));

if (drive.HasFailed)
{
    Console.WriteLine($"Drive failed: {drive.FailureReason}");
}
```

`WaitForCompletedAsync` throws `TimeoutException` if the action does not complete within the
requested timeout. Passing no timeout waits indefinitely.

## 2. Subscribe to robot events

**[SPEC]**
Register a typed handler with `AddHandler<TEvent>`. The returned `Handler` implements
`IDisposable`; use a `using` declaration to remove it when it is no longer needed.

```csharp
using Handler stateHandler = robot.AddHandler<EvtRobotStateUpdated>(_ =>
{
    Console.WriteLine($"Head angle: {robot.HeadAngle?.Degrees:F1} degrees");
});
```

`Robot` dispatches its events through the same handler API for state, object, face, and action
events. The [events sample](https://github.com/legendaryleo7/cozmo-csharp-sdk/blob/master/samples/03_events/Program.cs)
shows several subscriptions.

## 3. Wait for one event

**[SPEC]**
Use `WaitFor<TEvent>` when code should continue after the next matching event instead of
running a callback.

```csharp
EvtFaceAppeared appeared = await robot.WaitFor<EvtFaceAppeared>(
    timeout: TimeSpan.FromSeconds(30));

Console.WriteLine($"Saw face {appeared.Face.FaceId}");
```

If the event can occur before the wait is registered, register the wait before starting the
operation that may raise it.

## 4. Register an action completion handler

**[SPEC]**
Register `OnCompleted` before dispatch when completion should trigger a callback. Constructing
the action directly makes it possible to attach the handler before sending it.

```csharp
var action = new DriveStraight(
    Distance.FromMillimeters(50),
    Speed.FromMillimetersPerSecond(50),
    shouldPlayAnim: false);

using Handler completionHandler = action.OnCompleted(evt =>
    Console.WriteLine($"Action {evt.State}: {evt.FailureReason}"));

await robot.ActionDispatcher.SendSingleActionAsync(action);
await action.WaitForCompletedAsync(TimeSpan.FromSeconds(15));
```

## Changelog

- 1.0.0: Initial guide.
