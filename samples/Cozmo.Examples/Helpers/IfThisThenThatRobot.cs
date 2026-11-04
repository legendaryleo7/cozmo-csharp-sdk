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

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples;

/// <summary>Robot helpers shared by the IFTTT examples.</summary>
/// <remarks>
/// The Python examples subclass <c>Robot</c> in <c>common/__init__.py</c>. The C# robot is
/// sealed, so the same operations are provided as extension-style static helpers.
/// </remarks>
public static class IfThisThenThatRobot
{
    /// <summary>Plays the startled reaction, speaks the update, and shows an image.</summary>
    public static async Task RunReactionAsync(
        Robot robot,
        string speech,
        string imageFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(robot);
        try
        {
            await using PerformOffChargerContext charger = await robot.PerformOffChargerAsync();
            await FacePositioning.GetInPositionAsync(robot);

            AnimationTrigger startled = await robot.PlayAnimationTriggerAsync(
                Triggers.ReactToPokeStartled);
            await startled.WaitForCompletedAsync();

            SayText say = await robot.SayTextAsync(speech);
            await say.WaitForCompletedAsync();

            await DisplayImageFileOnFaceAsync(robot, imageFileName, cancellationToken);
        }
        catch (RobotBusyException)
        {
            Console.WriteLine($"Robot was busy so didn't read update: {speech}");
        }
    }

    /// <summary>Loads an image, fits it to the OLED, and displays it for five seconds.</summary>
    public static async Task DisplayImageFileOnFaceAsync(
        Robot robot,
        string imageFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(robot);
        (int width, int height) = OledFace.Dimensions;
        byte[] face = ImageLoader.ToScreenData(
            ImageLoader.Load(ExamplePaths.FaceImage(imageFileName)),
            width,
            height,
            ResamplingMode.Nearest,
            invert: true);
        await robot.DisplayOledFaceImageAsync(face, 5000);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
