// Copyright (c) 2016 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Port of examples/apps/desk_security_guard.py.
//
// The Python example uses the Tk viewer. The C# SDK has no viewer, so this port keeps the
// patrol and face-reaction logic and prints its status to the console.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Apps;

/// <summary>Patrol a desk and react to the enrolled owner or to unknown faces.</summary>
public static class DeskSecurityGuardProgram
{
    /// <summary>Runs the example until cancelled.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string ownerName = Environment.GetEnvironmentVariable("COZMO_OWNER_FACE") ?? string.Empty;
        if (ownerName.Length == 0)
        {
            Console.Error.WriteLine(
                "Set COZMO_OWNER_FACE to the enrolled owner's face name before running this example.");
            return Task.CompletedTask;
        }

        return ExampleRunner.RunAsync(
            async robot =>
            {
                await robot.Camera.SetImageStreamEnabledAsync(true);
                try
                {
                    var guard = new DeskSecurityGuard(ownerName);
                    if (robot.IsOnCharger)
                    {
                        DriveOffChargerContacts off = await robot.DriveOffChargerContactsAsync();
                        await off.WaitForCompletedAsync();
                        DriveStraight forward = await robot.DriveStraightAsync(
                            Distance.FromMillimeters(150),
                            Speed.FromMillimetersPerSecond(50));
                        await forward.WaitForCompletedAsync();
                    }

                    SetHeadAngle head = await robot.SetHeadAngleAsync(SetHeadAngle.MaxHeadAngle);
                    await head.WaitForCompletedAsync();

                    Angle initialPoseAngle = robot.PoseAngle ?? Angle.FromDegrees(0);
                    int patrolOffset = 0;
                    const double maxPoseAngle = 45.0;
                    const double timeBetweenTurns = 2.5;
                    const double timeBetweenPatrols = 20.0;

                    DateTimeOffset nextTurn = DateTimeOffset.UtcNow.AddSeconds(timeBetweenTurns);
                    DateTimeOffset nextPatrol = DateTimeOffset.UtcNow.AddSeconds(timeBetweenPatrols);

                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        DateTimeOffset now = DateTimeOffset.UtcNow;
                        if (now > nextTurn && !guard.IsInvestigatingIntruder())
                        {
                            int angleToTurn = Random.Shared.Next(10, 41);
                            if (Random.Shared.Next(2) > 0)
                            {
                                angleToTurn = -angleToTurn;
                            }

                            double faceAngle = ((robot.PoseAngle ?? initialPoseAngle) - initialPoseAngle).Degrees;
                            faceAngle += angleToTurn;
                            if (faceAngle > maxPoseAngle)
                            {
                                angleToTurn -= (int)(faceAngle - maxPoseAngle);
                            }
                            else if (faceAngle < -maxPoseAngle)
                            {
                                angleToTurn -= (int)(faceAngle + maxPoseAngle);
                            }

                            TurnInPlace turn = await robot.TurnInPlaceAsync(Util.Degrees(angleToTurn));
                            await turn.WaitForCompletedAsync();
                            SetHeadAngle look = await robot.SetHeadAngleAsync(
                                Util.Degrees(Random.Shared.Next(30, 45)));
                            await look.WaitForCompletedAsync();
                            nextTurn = DateTimeOffset.UtcNow.AddSeconds(timeBetweenTurns);
                        }

                        if (now > nextPatrol && !guard.IsInvestigatingIntruder())
                        {
                            double faceAngle = ((robot.PoseAngle ?? initialPoseAngle) - initialPoseAngle).Degrees;
                            bool driveRight = patrolOffset < 0 ||
                                (patrolOffset == 0 && faceAngle > 0);
                            if (driveRight)
                            {
                                TurnInPlace turn = await robot.TurnInPlaceAsync(
                                    Util.Degrees(90 - faceAngle));
                                await turn.WaitForCompletedAsync();
                                patrolOffset++;
                            }
                            else
                            {
                                TurnInPlace turn = await robot.TurnInPlaceAsync(
                                    Util.Degrees(-90 - faceAngle));
                                await turn.WaitForCompletedAsync();
                                patrolOffset--;
                            }

                            await robot.DriveWheelMotorsAsync(20, 20);
                            for (int index = 1; index <= 3; index++)
                            {
                                Animation patrolAnimation = await robot.PlayAnimationAsync(
                                    $"anim_hiking_driving_loop_0{index}");
                                await patrolAnimation.WaitForCompletedAsync();
                            }

                            await robot.StopAllMotorsAsync();

                            faceAngle = ((robot.PoseAngle ?? initialPoseAngle) - initialPoseAngle).Degrees;
                            TurnInPlace faceForward = await robot.TurnInPlaceAsync(
                                Util.Degrees(faceAngle > 0 ? -90 : 90));
                            await faceForward.WaitForCompletedAsync();
                            nextPatrol = DateTimeOffset.UtcNow.AddSeconds(timeBetweenPatrols);
                        }

                        await CheckForIntruderAsync(robot, guard);
                        await Task.Delay(TimeSpan.FromSeconds(0.05), context.CancellationToken);
                    }
                }
                finally
                {
                    await robot.Camera.SetImageStreamEnabledAsync(false);
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }

    private static async Task CheckForIntruderAsync(Robot robot, DeskSecurityGuard guard)
    {
        Face? ownerFace = null;
        Face? intruderFace = null;
        foreach (Face visibleFace in robot.World.VisibleFaces)
        {
            if (string.Equals(visibleFace.Name, guard.OwnerName, StringComparison.OrdinalIgnoreCase))
            {
                if (ownerFace is not null)
                {
                    Console.WriteLine(
                        $"Multiple faces with name {guard.OwnerName} seen - " +
                        $"{ownerFace.FaceId} and {visibleFace.FaceId}!");
                }

                ownerFace = visibleFace;
            }
            else
            {
                intruderFace ??= visibleFace;
            }
        }

        if (ownerFace is not null)
        {
            guard.LastObservedOwner = ownerFace.LastObservedTime;
            guard.FirstObservedOwner ??= guard.LastObservedOwner;
        }

        if (intruderFace is not null)
        {
            if (guard.LastObservedIntruder is null ||
                intruderFace.LastObservedTime > guard.LastObservedIntruder)
            {
                guard.LastObservedIntruder = intruderFace.LastObservedTime;
            }

            guard.FirstObservedIntruder ??= guard.LastObservedIntruder;
        }

        bool canSeeOwner = DidOccurRecently(guard.LastObservedOwner, 1.0);
        bool canSeeIntruders = DidOccurRecently(guard.LastObservedIntruder, 1.0);
        if (!guard.IsArmed)
        {
            canSeeIntruders = false;
        }

        if (!canSeeIntruders)
        {
            guard.FirstObservedIntruder = null;
        }

        if (canSeeOwner)
        {
            await robot.SetAllBackpackLightsAsync(Lights.Green);
            if (!DidOccurRecently(guard.LastAnnouncedOwner, 60.0))
            {
                AnimationTrigger greeting = await robot.PlayAnimationTriggerAsync(
                    Triggers.NamedFaceInitialGreeting);
                await greeting.WaitForCompletedAsync();
                guard.LastAnnouncedOwner = DateTimeOffset.UtcNow;
            }
            else if (ownerFace is not null)
            {
                TurnTowardsFace turn = await robot.TurnTowardsFaceAsync(ownerFace);
                await turn.WaitForCompletedAsync();
            }
        }
        else if (canSeeIntruders)
        {
            if (guard.HasConfirmedIntruder())
            {
                await robot.SetAllBackpackLightsAsync(Lights.Red);
                if (!DidOccurRecently(guard.LastAnnouncedIntruder, 10.0))
                {
                    SayText alert = await robot.SayTextAsync("Intruder Alert");
                    await alert.WaitForCompletedAsync();
                    guard.LastAnnouncedIntruder = DateTimeOffset.UtcNow;
                }

                if (!DidOccurRecently(guard.LastPouncedAtIntruder, 10.0))
                {
                    AnimationTrigger pounce = await robot.PlayAnimationTriggerAsync(
                        Triggers.CubePouncePounceNormal);
                    await pounce.WaitForCompletedAsync();
                    guard.LastPouncedAtIntruder = DateTimeOffset.UtcNow;
                }

                if (intruderFace is not null)
                {
                    TurnTowardsFace turn = await robot.TurnTowardsFaceAsync(intruderFace);
                    await turn.WaitForCompletedAsync();
                }
            }
            else
            {
                await robot.SetAllBackpackLightsAsync(Lights.Blue);
                if (!DidOccurRecently(guard.LastSuspicious, 10.0))
                {
                    AnimationTrigger suspicious = await robot.PlayAnimationTriggerAsync(
                        Triggers.HikingInterestingEdgeThought);
                    await suspicious.WaitForCompletedAsync();
                    guard.LastSuspicious = DateTimeOffset.UtcNow;
                }
                else if (intruderFace is not null)
                {
                    TurnTowardsFace turn = await robot.TurnTowardsFaceAsync(intruderFace);
                    await turn.WaitForCompletedAsync();
                }
            }
        }
        else
        {
            await robot.SetBackpackLightsOffAsync();
        }
    }

    private static bool DidOccurRecently(DateTimeOffset? eventTime, double maxElapsedSeconds)
        => eventTime is { } time &&
            (DateTimeOffset.UtcNow - time).TotalSeconds < maxElapsedSeconds;
}

/// <summary>Mutable status for the desk-security-guard example.</summary>
internal sealed class DeskSecurityGuard
{
    public DeskSecurityGuard(string ownerName)
    {
        OwnerName = ownerName;
    }

    public string OwnerName { get; }

    public bool IsArmed { get; set; } = true;

    public DateTimeOffset? FirstObservedIntruder { get; set; }

    public DateTimeOffset? LastObservedIntruder { get; set; }

    public DateTimeOffset? FirstObservedOwner { get; set; }

    public DateTimeOffset? LastObservedOwner { get; set; }

    public DateTimeOffset? LastSuspicious { get; set; }

    public DateTimeOffset? LastAnnouncedIntruder { get; set; }

    public DateTimeOffset? LastPouncedAtIntruder { get; set; }

    public DateTimeOffset? LastAnnouncedOwner { get; set; }

    public bool IsInvestigatingIntruder() => FirstObservedIntruder is not null;

    public bool HasConfirmedIntruder()
    {
        if (FirstObservedIntruder is not { } firstObserved)
        {
            return false;
        }

        return (DateTimeOffset.UtcNow - firstObserved).TotalSeconds > 2.0;
    }
}
