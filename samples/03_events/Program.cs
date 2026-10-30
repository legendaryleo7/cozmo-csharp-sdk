// Copyright (c) 2016-2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License in the file LICENSE.txt or at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Anki.Cozmo.Sdk;

// Subscribes to robot and world events and prints them for a while.
await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;

int stateUpdates = 0;
using Handler stateHandler = robot.AddHandler<EvtRobotStateUpdated>(_ =>
{
    // State updates arrive several times per second; print every thirtieth one.
    if (++stateUpdates % 30 == 0)
    {
        Console.WriteLine(
            $"head={robot.HeadAngle?.Degrees:F1} deg lift={robot.LiftRatio:F2} " +
            $"battery={robot.BatteryVoltage:F2} V onCharger={robot.IsOnCharger}");
    }
});

using Handler objectHandler = robot.AddHandler<EvtObjectAppeared>(evt =>
    Console.WriteLine($"Object appeared: {evt.Obj.DescriptiveName}"));

using Handler faceHandler = robot.AddHandler<EvtFaceAppeared>(evt =>
    Console.WriteLine($"Face appeared: id={evt.Face.FaceId} name='{evt.Face.Name}'"));

Console.WriteLine("Listening for 15 seconds...");
await Task.Delay(TimeSpan.FromSeconds(15));
Console.WriteLine("Done.");
