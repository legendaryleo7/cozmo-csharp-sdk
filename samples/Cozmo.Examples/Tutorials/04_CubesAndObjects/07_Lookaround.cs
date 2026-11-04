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

// Port of examples/tutorials/04_cubes_and_objects/07_lookaround.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Look around, then pick up and put down a cube.</summary>
public static class Lookaround
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                Behavior lookAround = await robot.StartBehaviorAsync(BehaviorTypes.LookAroundInPlace);
                LightCube? cube = null;
                try
                {
                    cube = await robot.World.WaitForObservedLightCubeAsync(
                        TimeSpan.FromSeconds(30));
                    Console.WriteLine($"Found cube {cube.DescriptiveName}");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Didn't find a cube :-(");
                }
                finally
                {
                    await lookAround.StopAsync();
                }

                if (cube is null)
                {
                    await robot.PlayAnimationTriggerAsync(Triggers.MajorFail);
                    return;
                }

                Console.WriteLine("Yay, found cube");
                await cube.SetLightsAsync(Lights.Green.Flash());

                AnimationTrigger react = await robot.PlayAnimationTriggerAsync(Triggers.BlockReact);
                await react.WaitForCompletedAsync();

                PickupObject pickup = await robot.PickupObjectAsync(cube);
                Console.WriteLine($"got action {pickup}");
                await pickup.WaitForCompletedAsync(TimeSpan.FromSeconds(30));

                TurnInPlace turn = await robot.TurnInPlaceAsync(Util.Degrees(90));
                await turn.WaitForCompletedAsync();

                PlaceObjectOnGroundHere place = await robot.PlaceObjectOnGroundHereAsync(cube);
                Console.WriteLine($"got action {place}");
                await place.WaitForCompletedAsync(TimeSpan.FromSeconds(30));

                AnimationTrigger win = await robot.PlayAnimationTriggerAsync(Triggers.MajorWin);
                await cube.SetLightCornersAsync(null, null, null, null);
                await win.WaitForCompletedAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
