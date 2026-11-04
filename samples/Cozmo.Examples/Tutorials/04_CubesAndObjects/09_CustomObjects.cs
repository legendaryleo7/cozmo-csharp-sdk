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

// Port of examples/tutorials/04_cubes_and_objects/09_custom_objects.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Define custom marker objects and report when they appear.</summary>
public static class CustomObjects
{
    /// <summary>Runs the example until cancelled.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                using Handler appeared = robot.AddHandler<EvtObjectAppeared>(evt =>
                {
                    if (evt.Obj is CustomObject custom)
                    {
                        Console.WriteLine($"Cozmo started seeing a {custom.ObjectType.Name}");
                    }
                });
                using Handler disappeared = robot.AddHandler<EvtObjectDisappeared>(evt =>
                {
                    if (evt.Obj is CustomObject custom)
                    {
                        Console.WriteLine($"Cozmo stopped seeing a {custom.ObjectType.Name}");
                    }
                });

                CustomObject? cube = await robot.World.DefineCustomCubeAsync(
                    CustomObjectTypes.CustomType00,
                    CustomObjectMarkers.Diamonds2,
                    44,
                    30,
                    30,
                    isUnique: true);
                CustomObject? bigCube = await robot.World.DefineCustomCubeAsync(
                    CustomObjectTypes.CustomType01,
                    CustomObjectMarkers.Diamonds3,
                    88,
                    50,
                    50,
                    isUnique: true);
                CustomObject? wall = await robot.World.DefineCustomWallAsync(
                    CustomObjectTypes.CustomType02,
                    CustomObjectMarkers.Circles2,
                    150,
                    120,
                    50,
                    30,
                    isUnique: true);
                CustomObject? box = await robot.World.DefineCustomBoxAsync(
                    CustomObjectTypes.CustomType03,
                    CustomObjectMarkers.Hexagons2,
                    CustomObjectMarkers.Circles3,
                    CustomObjectMarkers.Circles4,
                    CustomObjectMarkers.Circles5,
                    CustomObjectMarkers.Triangles2,
                    CustomObjectMarkers.Triangles3,
                    60,
                    140,
                    100,
                    30,
                    50,
                    isUnique: true);

                if (cube is not null && bigCube is not null && wall is not null && box is not null)
                {
                    Console.WriteLine("All objects defined successfully!");
                }
                else
                {
                    Console.WriteLine("One or more object definitions failed!");
                    return;
                }

                Console.WriteLine(
                    "Show the above markers to Cozmo; objects will be annotated in Cozmo's view " +
                    "and messages will be printed as they enter and exit view.");
                Console.WriteLine("Press CTRL-C to quit");
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.1), context.CancellationToken);
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
