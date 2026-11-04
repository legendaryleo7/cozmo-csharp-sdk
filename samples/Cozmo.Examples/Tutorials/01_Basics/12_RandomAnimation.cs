// Copyright (c) 2018 Anki, Inc.
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

// Port of examples/tutorials/01_basics/12_random_animation.py.

using Anki.Cozmo;
using Anki.Cozmo.Sdk;
using CladAnimationTrigger = Anki.Cozmo.AnimationTrigger;
using SdkAnimationTrigger = Anki.Cozmo.Sdk.AnimationTrigger;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Play three random triggers, then every trigger whose name contains "WinGame".</summary>
public static class RandomAnimation
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                List<CladEnum<CladAnimationTrigger>> allTriggers = robot.AnimTriggers.ToList();
                List<CladEnum<CladAnimationTrigger>> chosen = allTriggers
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(3)
                    .ToList();

                Console.WriteLine($"Playing {chosen.Count} random animations:");
                foreach (CladEnum<CladAnimationTrigger> trigger in chosen)
                {
                    Console.WriteLine($"Playing {trigger.Name}");
                    SdkAnimationTrigger action = await robot.PlayAnimationTriggerAsync(trigger);
                    await action.WaitForCompletedAsync();
                }

                IEnumerable<CladEnum<CladAnimationTrigger>> winningTriggers = allTriggers
                    .Where(trigger => trigger.Name.Contains("WinGame", StringComparison.Ordinal));

                foreach (CladEnum<CladAnimationTrigger> trigger in winningTriggers)
                {
                    Console.WriteLine($"Playing {trigger.Name}");
                    SdkAnimationTrigger action = await robot.PlayAnimationTriggerAsync(trigger);
                    await action.WaitForCompletedAsync();
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
