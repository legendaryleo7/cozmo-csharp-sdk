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

// Port of examples/tutorials/02_cozmo_face/02_cozmo_face_mirror.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CozmoFace;

/// <summary>Display Cozmo's camera feed back on his face like a mirror.</summary>
public static class CozmoFaceMirror
{
    /// <summary>Runs the example until cancelled.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                await robot.Camera.SetImageStreamEnabledAsync(true);
                try
                {
                    await FacePositioning.GetInPositionAsync(robot);

                    Console.WriteLine("Press CTRL-C to quit");
                    const int width = OledFace.ScreenWidth;
                    const int height = OledFace.ScreenHalfHeight;
                    TimeSpan frameDuration = TimeSpan.FromSeconds(0.1);

                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        CameraImage? latestImage = robot.World.LatestImage;
                        if (latestImage is not null)
                        {
                            ImageData frame = ImageLoader.FromCameraImage(latestImage);
                            ImageData resized = ImageLoader.Resize(
                                frame,
                                width,
                                height,
                                ResamplingMode.Bicubic);
                            ImageData mirrored = ImageLoader.FlipHorizontal(resized);
                            byte[] grayscale = ImageLoader.ToGrayscale(mirrored);
                            byte threshold = (byte)Math.Clamp(
                                (int)Math.Round(ImageLoader.MeanGrayscale(grayscale)),
                                0,
                                255);

                            byte[] screenData = ImageLoader.ToScreenData(
                                grayscale,
                                width,
                                height,
                                invert: false,
                                threshold);

                            await robot.DisplayOledFaceImageAsync(
                                screenData,
                                (uint)frameDuration.TotalMilliseconds);
                        }

                        await Task.Delay(frameDuration, context.CancellationToken);
                    }
                }
                finally
                {
                    await robot.Camera.SetImageStreamEnabledAsync(false);
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }
}
