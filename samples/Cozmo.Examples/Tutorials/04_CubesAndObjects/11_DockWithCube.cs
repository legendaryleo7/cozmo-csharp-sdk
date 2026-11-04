// Copyright (c) 2017 Anki, Inc.
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

// Port of examples/tutorials/04_cubes_and_objects/11_dock_with_cube.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Drive up to and dock with a cube.</summary>
public static class DockWithCube
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                SetHeadAngle head = await robot.SetHeadAngleAsync(Util.Degrees(-5.0));
                await head.WaitForCompletedAsync();

                Console.WriteLine("Cozmo is waiting until he sees a cube.");
                LightCube cube = await robot.World.WaitForObservedLightCubeAsync();

                Console.WriteLine("Cozmo found a cube, and will now attempt to dock with it:");
                Anki.Cozmo.Sdk.DockWithCube action = await robot.DockWithCubeAsync(
                    cube,
                    approachAngle: Util.Degrees(180),
                    numRetries: 2);
                await action.WaitForCompletedAsync();
                Console.WriteLine($"result: {action.Result?.Name}");
            },
            cancellationToken: context.CancellationToken);
    }
}
