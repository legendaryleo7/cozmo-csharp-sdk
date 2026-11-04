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

// Port of examples/tutorials/02_cozmo_face/01_cozmo_face_image.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CozmoFace;

/// <summary>Display two images on Cozmo's OLED face.</summary>
public static class CozmoFaceImage
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                (int width, int height) = OledFace.Dimensions;
                await FacePositioning.GetInPositionAsync(robot);

                byte[] sdkImage = ImageLoader.ToScreenData(
                    ImageLoader.Load(ExamplePaths.FaceImage("cozmosdk.png")),
                    width,
                    height,
                    ResamplingMode.Bicubic,
                    invert: true);
                byte[] helloImage = ImageLoader.ToScreenData(
                    ImageLoader.Load(ExamplePaths.FaceImage("hello_world.png")),
                    width,
                    height,
                    ResamplingMode.Nearest,
                    invert: true);

                const int loops = 10;
                const double durationSeconds = 2.0;
                Console.WriteLine(
                    $"Press CTRL-C to quit (or wait {loops * durationSeconds:0} seconds to complete)");

                for (int loop = 0; loop < loops; loop++)
                {
                    foreach (byte[] screenData in new[] { sdkImage, helloImage })
                    {
                        await robot.DisplayOledFaceImageAsync(
                            screenData,
                            (uint)(durationSeconds * 1000.0));
                        await Task.Delay(
                            TimeSpan.FromSeconds(durationSeconds),
                            context.CancellationToken);
                    }
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }
}
