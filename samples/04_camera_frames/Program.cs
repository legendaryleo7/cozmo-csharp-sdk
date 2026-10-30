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

// Enables the camera, saves five JPEG frames and disables the stream again.
await using CozmoRunner runner = await CozmoRunner.ConnectAsync();
Robot robot = runner.Robot;

string outputDirectory = Path.Combine(Environment.CurrentDirectory, "frames");
Directory.CreateDirectory(outputDirectory);
Console.WriteLine($"Saving frames to {outputDirectory}");

await robot.Camera.SetImageStreamEnabledAsync(true);
try
{
    for (int index = 0; index < 5; index++)
    {
        EvtNewRawCameraImage frame = await robot.Camera.WaitFor<EvtNewRawCameraImage>(
            timeout: TimeSpan.FromSeconds(10));
        string path = Path.Combine(outputDirectory, $"frame_{frame.Image.ImageNumber:D4}.jpg");
        await File.WriteAllBytesAsync(path, frame.Image.RawImage);
        Console.WriteLine($"Saved {path}");
    }
}
finally
{
    await robot.Camera.SetImageStreamEnabledAsync(false);
}

Console.WriteLine("Done.");
