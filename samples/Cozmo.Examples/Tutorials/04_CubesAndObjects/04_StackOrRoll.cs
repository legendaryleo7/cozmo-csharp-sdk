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

// Port of examples/tutorials/04_cubes_and_objects/04_stack_or_roll.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>React differently depending on how many cubes Cozmo sees.</summary>
public static class StackOrRoll
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
                        timeout: TimeSpan.FromSeconds(10));
                Console.WriteLine($"Found {cubes.Count} cubes");
                await lookAround.StopAsync();

                if (cubes.Count == 0)
                {
                    AnimationTrigger fail = await robot.PlayAnimationTriggerAsync(Triggers.MajorFail);
                    await fail.WaitForCompletedAsync();
                }
                else if (cubes.Count == 1)
                {
                    await robot.RunTimedBehaviorAsync(
                        BehaviorTypes.RollBlock,
                        TimeSpan.FromSeconds(60));
                }
                else
                {
                    await robot.RunTimedBehaviorAsync(
                        BehaviorTypes.StackBlocks,
                        TimeSpan.FromSeconds(60));
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
