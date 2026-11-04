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

// Port of examples/multi_robot/multi_robot_independent.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.MultiRobot;

/// <summary>Run independent routines on two Cozmos concurrently.</summary>
public static class MultiRobotIndependent
{
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using MultiRobotConnection connection1 =
            await MultiRobotConnection.ConnectAsync(context.Arguments, 1, context.CancellationToken);
        await using MultiRobotConnection connection2 =
            await MultiRobotConnection.ConnectAsync(context.Arguments, 2, context.CancellationToken);

        Task first = TurnAsync(connection1.Runner.Robot, 1, Util.Degrees(90));
        Task second = TurnAsync(connection2.Runner.Robot, 2, Util.Degrees(-90));
        await Task.WhenAll(first, second);
    }

    private static async Task TurnAsync(Robot robot, int index, Angle angle)
    {
        Console.WriteLine($"Turning robot {index}");
        TurnInPlace action = await robot.TurnInPlaceAsync(angle);
        await action.WaitForCompletedAsync();
    }
}
