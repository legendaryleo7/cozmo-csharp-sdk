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

// Waits for a light cube to become visible and animates its LEDs.
await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;

Console.WriteLine("Waiting for a light cube to be observed...");
LightCube cube = await robot.World.WaitForObservedLightCubeAsync(TimeSpan.FromSeconds(30));
Console.WriteLine($"Observed {cube.CubeId.Name} (object id {cube.ObjectId}).");

await cube.SetLightsAsync(Lights.Green);
await Task.Delay(2000);

await cube.SetLightCornersAsync(Lights.Red, Lights.Green, Lights.Blue, Lights.White);
await Task.Delay(2000);

await cube.SetLightsOffAsync();
Console.WriteLine("Cube lights turned off.");
