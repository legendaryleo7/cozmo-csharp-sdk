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

// Port of examples/tutorials/04_cubes_and_objects/08_drive_to_charger_test.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Find the charger and drive to within 65 mm of it.</summary>
public static class DriveToChargerTest
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                if (robot.IsOnCharger)
                {
                    DriveOffChargerContacts off = await robot.DriveOffChargerContactsAsync();
                    await off.WaitForCompletedAsync();
                    DriveStraight forward = await robot.DriveStraightAsync(
                        Distance.FromMillimeters(100),
                        Speed.FromMillimetersPerSecond(50));
                    await forward.WaitForCompletedAsync();
                    await robot.MoveLiftAsync(-3);
                    TurnInPlace turn = await robot.TurnInPlaceAsync(Util.Degrees(180));
                    await turn.WaitForCompletedAsync();
                    SetHeadAngle head = await robot.SetHeadAngleAsync(Util.Degrees(0));
                    await head.WaitForCompletedAsync();
                    await Task.Delay(TimeSpan.FromSeconds(0.5), context.CancellationToken);
                    DriveStraight backward = await robot.DriveStraightAsync(
                        Distance.FromMillimeters(-60),
                        Speed.FromMillimetersPerSecond(50));
                    await backward.WaitForCompletedAsync();
                }

                Charger? charger = null;
                if (robot.World.Charger is { } knownCharger &&
                    robot.Pose is { } robotPose &&
                    knownCharger.Pose is { } chargerPose &&
                    chargerPose.IsComparable(robotPose))
                {
                    Console.WriteLine("Cozmo already knows where the charger is!");
                    charger = knownCharger;
                }

                if (charger is null)
                {
                    Behavior lookAround = await robot.StartBehaviorAsync(BehaviorTypes.LookAroundInPlace);
                    try
                    {
                        charger = await robot.World.WaitForObservedChargerAsync(
                            TimeSpan.FromSeconds(30));
                        Console.WriteLine($"Found charger: {charger.DescriptiveName}");
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Didn't see the charger");
                    }
                    finally
                    {
                        await lookAround.StopAsync();
                    }
                }

                if (charger is not null)
                {
                    GoToObject action = await robot.GoToObjectAsync(
                        charger,
                        Distance.FromMillimeters(65.0));
                    await action.WaitForCompletedAsync();
                    Console.WriteLine($"Completed action: result = {action.Result?.Name}");
                    Console.WriteLine("Done.");
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }
}
