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

// Port of examples/tutorials/01_basics/09_cube_lights.py.

using Anki.Cozmo;
using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Set the three light cubes to red, green and blue.</summary>
public static class CubeLights
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                LightCube? cube1 = CubeLookup.GetLightCubeOrNull(robot.World, CustomObjectTypes.LightCube1Id);
                LightCube? cube2 = CubeLookup.GetLightCubeOrNull(robot.World, CustomObjectTypes.LightCube2Id);
                LightCube? cube3 = CubeLookup.GetLightCubeOrNull(robot.World, CustomObjectTypes.LightCube3Id);

                await SetOrWarnAsync(cube1, Lights.Red, "LightCube1Id");
                await SetOrWarnAsync(cube2, Lights.Green, "LightCube2Id");
                await SetOrWarnAsync(cube3, Lights.Blue, "LightCube3Id");

                await Task.Delay(TimeSpan.FromSeconds(10), context.CancellationToken);
            },
            cancellationToken: context.CancellationToken);
    }

    private static async Task SetOrWarnAsync(LightCube? cube, Light light, string name)
    {
        if (cube is null)
        {
            Console.WriteLine($"Cozmo is not connected to a {name} cube - check the battery.");
            return;
        }

        await cube.SetLightsAsync(light);
    }
}
