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

// Port of examples/tutorials/01_basics/08_animation.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Play animations by trigger and by name.</summary>
public static class Animation
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                Console.WriteLine("Playing Animation Trigger 1:");
                AnimationTrigger first = await robot.PlayAnimationTriggerAsync(
                    Triggers.CubePounceLoseSession);
                await first.WaitForCompletedAsync();

                Console.WriteLine("Playing Animation Trigger 2: (Ignoring the body track)");
                AnimationTrigger second = await robot.PlayAnimationTriggerAsync(
                    Triggers.CubePounceLoseSession,
                    ignoreBodyTrack: true);
                await second.WaitForCompletedAsync();

                Console.WriteLine("Playing Animation 3:");
                Anki.Cozmo.Sdk.Animation third = await robot.PlayAnimationAsync("anim_poked_giggle");
                await third.WaitForCompletedAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
