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

// Port of examples/tutorials/01_basics/04_drive_square.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Make Cozmo drive in a square.</summary>
public static class DriveSquare
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                for (int side = 0; side < 4; side++)
                {
                    DriveStraight drive = await robot.DriveStraightAsync(
                        Distance.FromMillimeters(150),
                        Speed.FromMillimetersPerSecond(50));
                    await drive.WaitForCompletedAsync();

                    TurnInPlace turn = await robot.TurnInPlaceAsync(Util.Degrees(90));
                    await turn.WaitForCompletedAsync();
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
