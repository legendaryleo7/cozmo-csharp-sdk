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

// Connects to the first available Cozmo, makes it speak, drive and turn.
await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;
Console.WriteLine($"Connected to robot {robot.Serial} (id {robot.RobotId}).");

SayText hello = await robot.SayTextAsync("Hello world");
await hello.WaitForCompletedAsync(TimeSpan.FromSeconds(15));

DriveStraight drive = await robot.DriveStraightAsync(
    Distance.FromMillimeters(50),
    Speed.FromMillimetersPerSecond(50));
await drive.WaitForCompletedAsync(TimeSpan.FromSeconds(15));

TurnInPlace turn = await robot.TurnInPlaceAsync(Util.Degrees(90));
await turn.WaitForCompletedAsync(TimeSpan.FromSeconds(15));

await robot.SetAllBackpackLightsAsync(Lights.Green);
Console.WriteLine("Done.");
