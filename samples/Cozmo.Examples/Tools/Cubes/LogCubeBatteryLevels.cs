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

// Port of examples/tools/cubes/log_cube_battery_levels.py.

using Anki.Cozmo;
using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tools.Cubes;

/// <summary>Log the battery level of each connected cube.</summary>
public static class LogCubeBatteryLevels
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                foreach (CladEnum<ObjectType> cubeId in CubeLookup.CubeIds)
                {
                    await LogCubeInfoAsync(robot, cubeId, context.CancellationToken);
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }

    private static async Task LogCubeInfoAsync(
        Robot robot,
        CladEnum<ObjectType> cubeId,
        CancellationToken cancellationToken)
    {
        LightCube? cube = CubeLookup.GetLightCubeOrNull(robot.World, cubeId);
        if (cube is null)
        {
            Console.WriteLine($"Cube {cubeId.Name} is not connected - check the battery.");
            return;
        }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            if (cube.BatteryVoltage is not null)
            {
                break;
            }

            if (attempt == 0)
            {
                Console.WriteLine($"Cube {cubeId.Name} waiting for battery info...");
            }

            await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
        }

        Console.WriteLine($"Cube {cubeId.Name} battery = {cube.BatteryStr}");
    }
}
