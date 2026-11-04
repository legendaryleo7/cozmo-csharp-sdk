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

// Port of examples/tutorials/04_cubes_and_objects/02_create_wall.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Create an invisible wall and drive around it.</summary>
public static class CreateWall
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                FixedCustomObject wall = await robot.World.CreateCustomFixedObjectAsync(
                    Pose.FromZAngle(100, 0, 0, Util.Degrees(0)),
                    10,
                    100,
                    100,
                    relativeToRobot: true);
                Console.WriteLine($"fixed_object created successfully: {wall.ObjectId}");

                GoToPose action = await robot.GoToPoseAsync(
                    Pose.FromZAngle(200, 0, 0, Util.Degrees(0)),
                    relativeToRobot: true);
                await action.WaitForCompletedAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
