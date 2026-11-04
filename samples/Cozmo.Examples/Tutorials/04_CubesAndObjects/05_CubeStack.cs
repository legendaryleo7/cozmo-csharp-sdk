// Copyright (c) 2016-2017 Anki, Inc.
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

// Port of examples/tutorials/04_cubes_and_objects/05_cube_stack.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Pick up one cube and place it on another.</summary>
public static class CubeStack
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
                        num: 2,
                        objectType: typeof(LightCube),
                        timeout: TimeSpan.FromSeconds(60));
                await lookAround.StopAsync();

                if (cubes.Count < 2)
                {
                    Console.WriteLine(
                        $"Error: need 2 Cubes but only found {cubes.Count} Cube(s)");
                    return;
                }

                PickupObject pickup = await robot.PickupObjectAsync(cubes[0], numRetries: 3);
                await pickup.WaitForCompletedAsync();
                if (pickup.HasFailed)
                {
                    Console.WriteLine(
                        $"Pickup Cube failed: code={pickup.FailureCode} " +
                        $"reason='{pickup.FailureReason}' result={pickup.Result?.Name}");
                    return;
                }

                PlaceOnObject place = await robot.PlaceOnObjectAsync(cubes[1], numRetries: 3);
                await place.WaitForCompletedAsync();
                if (place.HasFailed)
                {
                    Console.WriteLine(
                        $"Place On Cube failed: code={place.FailureCode} " +
                        $"reason='{place.FailureReason}' result={place.Result?.Name}");
                    return;
                }

                Console.WriteLine("Cozmo successfully stacked 2 blocks!");
            },
            cancellationToken: context.CancellationToken);
    }
}
