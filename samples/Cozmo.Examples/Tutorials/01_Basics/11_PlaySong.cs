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

// Port of examples/tutorials/01_basics/11_play_song.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Play a rising scale and the same pitch at different durations.</summary>
public static class PlaySong
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                SongNote[] scale =
                [
                    new(NoteTypes.C2, NoteDurations.Quarter),
                    new(NoteTypes.C2_Sharp, NoteDurations.Quarter),
                    new(NoteTypes.D2, NoteDurations.Quarter),
                    new(NoteTypes.D2_Sharp, NoteDurations.Quarter),
                    new(NoteTypes.E2, NoteDurations.Quarter),
                    new(NoteTypes.F2, NoteDurations.Quarter),
                    new(NoteTypes.F2_Sharp, NoteDurations.Quarter),
                    new(NoteTypes.G2, NoteDurations.Quarter),
                    new(NoteTypes.G2_Sharp, NoteDurations.Quarter),
                    new(NoteTypes.A2, NoteDurations.Quarter),
                    new(NoteTypes.A2_Sharp, NoteDurations.Quarter),
                    new(NoteTypes.B2, NoteDurations.Quarter),
                    new(NoteTypes.C3, NoteDurations.Quarter),
                    new(NoteTypes.C3_Sharp, NoteDurations.Quarter),
                    new(NoteTypes.Rest, NoteDurations.Quarter),
                ];

                Anki.Cozmo.Sdk.Animation scaleAction = await robot.PlaySongAsync(scale, loopCount: 1);
                await scaleAction.WaitForCompletedAsync();

                SongNote[] durations =
                [
                    new(NoteTypes.C3, NoteDurations.Half),
                    new(NoteTypes.C3, NoteDurations.ThreeQuarter),
                    new(NoteTypes.Rest, NoteDurations.Quarter),
                    new(NoteTypes.C3, NoteDurations.Quarter),
                    new(NoteTypes.C3, NoteDurations.Whole),
                ];

                Anki.Cozmo.Sdk.Animation durationAction = await robot.PlaySongAsync(durations, loopCount: 1);
                await durationAction.WaitForCompletedAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
