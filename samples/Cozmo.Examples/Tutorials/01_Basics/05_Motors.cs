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

// Port of examples/tutorials/01_basics/05_motors.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Drive the wheels, lift and head motors directly.</summary>
public static class Motors
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                // Head down, lift down, and drive forwards while turning left.
                await robot.MoveHeadAsync(-5);
                await robot.MoveLiftAsync(-5);
                await robot.DriveWheelMotorsAsync(25, 50);
                await Task.Delay(TimeSpan.FromSeconds(3), context.CancellationToken);

                // Head up, lift up, and turn in place to the right.
                await robot.MoveHeadAsync(5);
                await robot.MoveLiftAsync(5);
                await robot.DriveWheelMotorsAsync(50, -50);
                await Task.Delay(TimeSpan.FromSeconds(3), context.CancellationToken);

                await robot.StopAllMotorsAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
