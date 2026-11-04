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

// Port of examples/tutorials/04_cubes_and_objects/06_pickup_furthest.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Pick up the cube that is furthest from the robot.</summary>
public static class PickupFurthest
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                Behavior lookAround = await robot.StartBehaviorAsync(BehaviorTypes.LookAroundInPlace);
                IReadOnlyList<ObservableObject> cubes =
                    await robot.World.WaitUntilObserveNumObjectsAsync(
                        num: 3,
                        objectType: typeof(LightCube),
                        timeout: TimeSpan.FromSeconds(60));
                await lookAround.StopAsync();

                double maximumDistance = 0.0;
                ObservableObject? target = null;
                foreach (ObservableObject cube in cubes)
                {
                    if (robot.Pose is not { } robotPose || cube.Pose is not { } cubePose)
                    {
                        continue;
                    }

                    Pose translation = robotPose - cubePose;
                    double distanceSquared =
                        (translation.Position.X * translation.Position.X) +
                        (translation.Position.Y * translation.Position.Y);
                    if (distanceSquared > maximumDistance)
                    {
                        maximumDistance = distanceSquared;
                        target = cube;
                    }
                }

                if (cubes.Count < 3)
                {
                    Console.WriteLine(
                        $"Error: need 3 Cubes but only found {cubes.Count} Cube(s)");
                    return;
                }

                if (target is null)
                {
                    Console.WriteLine("Error: no cube had a comparable pose.");
                    return;
                }

                PickupObject pickup = await robot.PickupObjectAsync(target, numRetries: 3);
                await pickup.WaitForCompletedAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
