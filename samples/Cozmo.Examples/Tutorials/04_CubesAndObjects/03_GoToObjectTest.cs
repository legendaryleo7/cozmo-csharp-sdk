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

// Port of examples/tutorials/04_cubes_and_objects/03_go_to_object_test.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Find a light cube and drive to within 70 mm of it.</summary>
public static class GoToObjectTest
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                await robot.MoveLiftAsync(-3);
                SetHeadAngle head = await robot.SetHeadAngleAsync(Util.Degrees(0));
                await head.WaitForCompletedAsync();

                Behavior lookAround = await robot.StartBehaviorAsync(BehaviorTypes.LookAroundInPlace);
                LightCube? cube = null;
                try
                {
                    cube = await robot.World.WaitForObservedLightCubeAsync(
                        TimeSpan.FromSeconds(30));
                    Console.WriteLine($"Found cube: {cube.DescriptiveName}");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Didn't find a cube");
                }
                finally
                {
                    await lookAround.StopAsync();
                }

                if (cube is not null)
                {
                    GoToObject action = await robot.GoToObjectAsync(
                        cube,
                        Distance.FromMillimeters(70.0));
                    await action.WaitForCompletedAsync();
                    Console.WriteLine($"Completed action: result = {action.Result?.Name}");
                    Console.WriteLine("Done.");
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
