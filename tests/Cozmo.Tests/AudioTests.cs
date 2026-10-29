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

using System.Reflection;
using Anki.AudioMetaData.GameEvent;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using CladSongNote = Anki.Cozmo.ExternalInterface.SongNote;
using SongNote = Anki.Cozmo.Sdk.SongNote;

namespace Cozmo.Tests;

public sealed class AudioTests
{
    [Fact]
    public void AudioEventsExposeEveryCladCodelabEntry()
    {
        FieldInfo[] fields = typeof(AudioEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(CladEnum<Codelab>))
            .ToArray();

        // 133 entries are declared in audio.py; Python's CladEnumWrapper auto-adds the
        // remaining 37 CLAD members under their raw names (audio.py:379).
        Assert.Equal(170, fields.Length);
        foreach (FieldInfo field in fields)
        {
            var entry = (CladEnum<Codelab>)field.GetValue(null)!;
            // The field name is the Python public name; for renamed entries it differs
            // from the CLAD member name that CladEnum.Name reports.
            var cladValue = Enum.Parse<Codelab>(entry.Name);
            Assert.Equal((long)cladValue, entry.Id);
            Assert.Equal(cladValue, entry.Value);
        }
    }

    [Theory]
    [InlineData("MusicGlobalStop", "Music_Global_Stop")]
    [InlineData("MusicBackgroundSilenceOn", "Music_Background_Silence_On")]
    [InlineData("MusicTinyOrchestraBassMode1Stop", "Music_Tiny_Orchestra_Bass_Mode_1_Stop")]
    [InlineData("MusicFunLoop", "Music_Fun_Loop")]
    [InlineData("MusicFunLoopStop", "Music_Fun_Loop_Stop")]
    [InlineData("SfxPuttHoleSuccess", "Sfx_Putt_Hole_Success")]
    [InlineData("SfxPuttHoleSuccessStop", "Sfx_Putt_Hole_Success_Stop")]
    public void AudioEventsPreservePythonPublicNames(string publicName, string cladName)
    {
        FieldInfo field = typeof(AudioEvents).GetField(publicName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"AudioEvents.{publicName} is missing.");
        var entry = (CladEnum<Codelab>)field.GetValue(null)!;

        Assert.Equal(cladName, entry.Name);
        Assert.Equal((long)Enum.Parse<Codelab>(cladName), entry.Id);
    }

    [Fact]
    public void AudioEventsExposePythonIds()
    {
        Assert.Equal(0L, AudioEvents.Invalid.Id);
        Assert.Equal(2512535841L, AudioEvents.MusicGlobalStop.Id);
        Assert.Equal(1284343309L, AudioEvents.MusicFunLoop.Id);
        Assert.Equal(2123694542L, AudioEvents.MusicFunLoopStop.Id);
        Assert.Equal("Sfx_Cube_Light_Stop", AudioEvents.SfxCubeLightStop.Name);
        // The 37 CLAD members Python leaves undeclared remain reachable by their CLAD names.
        Assert.Equal("Music_Minigame_Guarding", AudioEvents.Music_Minigame_Guarding.Name);
        Assert.Equal((long)Codelab.Music_Minigame_Guarding, AudioEvents.Music_Minigame_Guarding.Id);
        Assert.Equal("Sfx_Outdoor_Wolf", AudioEvents.Sfx_Outdoor_Wolf.Name);
    }

    [Fact]
    public void NoteTypesMirrorSongNoteType()
    {
        FieldInfo[] fields = typeof(NoteTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(CladEnum<SongNoteType>))
            .ToArray();

        Assert.Equal(15, fields.Length);
        foreach (FieldInfo field in fields)
        {
            var entry = (CladEnum<SongNoteType>)field.GetValue(null)!;
            Assert.Equal(field.Name, entry.Name);
            var cladValue = Enum.Parse<SongNoteType>(entry.Name);
            Assert.Equal((long)cladValue, entry.Id);
            Assert.Equal(cladValue, entry.Value);
        }

        Assert.Equal(0L, NoteTypes.C2.Id);
        Assert.Equal(13L, NoteTypes.C3_Sharp.Id);
        Assert.Equal(14L, NoteTypes.Rest.Id);
    }

    [Fact]
    public void NoteDurationsMirrorSongNoteDuration()
    {
        FieldInfo[] fields = typeof(NoteDurations)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(CladEnum<SongNoteDuration>))
            .ToArray();

        Assert.Equal(4, fields.Length);
        foreach (FieldInfo field in fields)
        {
            var entry = (CladEnum<SongNoteDuration>)field.GetValue(null)!;
            Assert.Equal(field.Name, entry.Name);
            var cladValue = Enum.Parse<SongNoteDuration>(entry.Name);
            Assert.Equal((long)cladValue, entry.Id);
            Assert.Equal(cladValue, entry.Value);
        }

        Assert.Equal(0L, NoteDurations.Whole.Id);
        Assert.Equal(1L, NoteDurations.ThreeQuarter.Id);
        Assert.Equal(3L, NoteDurations.Quarter.Id);
    }

    [Fact]
    public void SongNoteDefaultsMatchPython()
    {
        var note = new SongNote();

        Assert.Same(NoteTypes.C2, note.NoteType);
        Assert.Same(NoteDurations.Whole, note.NoteDuration);
        Assert.Equal(0L, note.NoteType.Id);
        Assert.Equal(0L, note.NoteDuration.Id);
        Assert.Contains("C2", note.ToString(), StringComparison.Ordinal);
        Assert.Contains("Whole", note.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SongNoteConvertsToCladMessage()
    {
        var note = new SongNote(NoteTypes.Rest, NoteDurations.Quarter);

        CladSongNote clad = note.ToClad();

        Assert.Equal((byte)14, clad.noteType);
        Assert.Equal((byte)3, clad.noteDuration);
    }

    [Fact]
    public void SongNoteEqualityUsesNoteTypeAndDuration()
    {
        var first = new SongNote(NoteTypes.A2, NoteDurations.Half);
        var second = new SongNote(NoteTypes.A2, NoteDurations.Half);
        var different = new SongNote(NoteTypes.A2, NoteDurations.Quarter);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.NotEqual(first, different);
        Assert.True(first != different);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
