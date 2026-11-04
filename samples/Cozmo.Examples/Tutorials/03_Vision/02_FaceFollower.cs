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

// Port of examples/tutorials/03_vision/02_face_follower.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Vision;

/// <summary>Turn towards a face continuously to keep it in frame.</summary>
public static class FaceFollower
{
    /// <summary>Runs the example until cancelled or no face is found.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                await robot.MoveLiftAsync(-3);
                SetHeadAngle head = await robot.SetHeadAngleAsync(SetHeadAngle.MaxHeadAngle);
                await head.WaitForCompletedAsync();

                Face? faceToFollow = null;
                Console.WriteLine("Press CTRL-C to quit");
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    TurnTowardsFace? turnAction = null;
                    if (faceToFollow is not null)
                    {
                        turnAction = await robot.TurnTowardsFaceAsync(faceToFollow);
                    }

                    if (faceToFollow is not { IsVisible: true })
                    {
                        try
                        {
                            faceToFollow = await robot.World.WaitForObservedFaceAsync(
                                TimeSpan.FromSeconds(30));
                        }
                        catch (TimeoutException)
                        {
                            Console.WriteLine("Didn't find a face - exiting!");
                            return;
                        }
                    }

                    if (turnAction is not null)
                    {
                        await turnAction.WaitForCompletedAsync();
                    }

                    await Task.Delay(TimeSpan.FromSeconds(0.1), context.CancellationToken);
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
