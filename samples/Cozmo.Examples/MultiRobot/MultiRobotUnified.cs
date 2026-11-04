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

// Port of examples/multi_robot/multi_robot_unified.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.MultiRobot;

/// <summary>Control two Cozmos from one routine.</summary>
public static class MultiRobotUnified
{
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using MultiRobotConnection connection1 =
            await MultiRobotConnection.ConnectAsync(context.Arguments, 1, context.CancellationToken);
        await using MultiRobotConnection connection2 =
            await MultiRobotConnection.ConnectAsync(context.Arguments, 2, context.CancellationToken);

        Robot robot1 = connection1.Runner.Robot;
        Robot robot2 = connection2.Runner.Robot;

        Console.WriteLine("Turning robot 1");
        TurnInPlace first = await robot1.TurnInPlaceAsync(Util.Degrees(90));
        await first.WaitForCompletedAsync();

        Console.WriteLine("Turning robot 2");
        TurnInPlace second = await robot2.TurnInPlaceAsync(Util.Degrees(-90));
        await second.WaitForCompletedAsync();

        Console.WriteLine("Turning both robots");
        TurnInPlace turn1 = await robot1.TurnInPlaceAsync(Util.Degrees(-90));
        TurnInPlace turn2 = await robot2.TurnInPlaceAsync(Util.Degrees(90));
        await turn1.WaitForCompletedAsync();
        await turn2.WaitForCompletedAsync();
    }
}
