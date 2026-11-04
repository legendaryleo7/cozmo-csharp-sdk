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

// Port of examples/tutorials/01_basics/10_play_sound.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Play one-off sounds and the tiny-orchestra loop.</summary>
public static class PlaySound
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                // A sound that ends on its own.
                await robot.PlayAudioAsync(AudioEvents.SfxGameWin);
                await Task.Delay(TimeSpan.FromSeconds(1.0), context.CancellationToken);

                // A loop that we interrupt after two seconds.
                await robot.PlayAudioAsync(AudioEvents.MusicStyle80S1159BpmLoop);
                await Task.Delay(TimeSpan.FromSeconds(2.0), context.CancellationToken);
                await robot.PlayAudioAsync(AudioEvents.MusicStyle80S1159BpmLoopStop);

                // The tiny-orchestra system adds synchronized channels until stopped.
                await robot.PlayAudioAsync(AudioEvents.MusicTinyOrchestraInit);
                await robot.PlayAudioAsync(AudioEvents.MusicTinyOrchestraBassMode1);
                await Task.Delay(TimeSpan.FromSeconds(5.0), context.CancellationToken);

                await robot.PlayAudioAsync(AudioEvents.MusicTinyOrchestraStringsMode3);
                await Task.Delay(TimeSpan.FromSeconds(5.0), context.CancellationToken);

                await robot.PlayAudioAsync(AudioEvents.MusicTinyOrchestraStop);
            },
            cancellationToken: context.CancellationToken);
    }
}
