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

// Port of examples/tutorials/04_cubes_and_objects/10_object_moved.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CubesAndObjects;

/// <summary>Print cube movement events.</summary>
public static class ObjectMoved
{
    /// <summary>Runs the example until cancelled.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                using Handler started = robot.AddHandler<EvtObjectMovingStarted>(evt =>
                    Console.WriteLine(
                        $"Object {evt.Obj.ObjectId} started moving: acceleration={evt.Acceleration}"));
                using Handler moving = robot.AddHandler<EvtObjectMoving>(evt =>
                    Console.WriteLine(
                        $"Object {evt.Obj.ObjectId} is moving: acceleration={evt.Acceleration}, " +
                        $"duration={evt.MoveDuration:0.0} seconds"));
                using Handler stopped = robot.AddHandler<EvtObjectMovingStopped>(evt =>
                    Console.WriteLine(
                        $"Object {evt.Obj.ObjectId} stopped moving: " +
                        $"duration={evt.MoveDuration:0.0} seconds"));

                Console.WriteLine("Press CTRL-C to quit");
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1.0), context.CancellationToken);
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }
}
