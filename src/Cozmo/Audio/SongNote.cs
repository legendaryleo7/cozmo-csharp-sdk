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

namespace Anki.Cozmo.Sdk;

/// <summary>One element of a song: a note type (pitch or rest) and a note duration.</summary>
/// <remarks>
/// <para>
/// This value object carries the note type and duration and converts to the CLAD message
/// with <see cref="ToClad"/>.
/// </para>
/// <para>
/// The default note type is <see cref="NoteTypes.C2"/> and the default duration is
/// <see cref="NoteDurations.Whole"/>.
/// </para>
/// </remarks>
public readonly struct SongNote : IEquatable<SongNote>
{
    private readonly CladEnum<global::Anki.Cozmo.ExternalInterface.SongNoteType>? _noteType;
    private readonly CladEnum<global::Anki.Cozmo.ExternalInterface.SongNoteDuration>? _noteDuration;

    /// <summary>Creates a song note with the default note type and duration.</summary>
    public SongNote()
        : this(null, null)
    {
    }

    /// <summary>Creates a song note.</summary>
    /// <param name="noteType">Pitch or rest; defaults to <see cref="NoteTypes.C2"/>.</param>
    /// <param name="noteDuration">Note length; defaults to <see cref="NoteDurations.Whole"/>.</param>
    public SongNote(
        CladEnum<global::Anki.Cozmo.ExternalInterface.SongNoteType>? noteType = null,
        CladEnum<global::Anki.Cozmo.ExternalInterface.SongNoteDuration>? noteDuration = null)
    {
        _noteType = noteType;
        _noteDuration = noteDuration;
    }

    /// <summary>Pitch or rest of this note; <see cref="NoteTypes.C2"/> for a default value.</summary>
    public CladEnum<global::Anki.Cozmo.ExternalInterface.SongNoteType> NoteType
        => _noteType ?? NoteTypes.C2;

    /// <summary>Length of this note; <see cref="NoteDurations.Whole"/> for a default value.</summary>
    public CladEnum<global::Anki.Cozmo.ExternalInterface.SongNoteDuration> NoteDuration
        => _noteDuration ?? NoteDurations.Whole;

    /// <summary>Converts this note to the CLAD message sent to the engine.</summary>
    public global::Anki.Cozmo.ExternalInterface.SongNote ToClad()
        => new((byte)NoteType.Id, (byte)NoteDuration.Id);

    /// <inheritdoc />
    public bool Equals(SongNote other)
        => Equals(NoteType, other.NoteType) && Equals(NoteDuration, other.NoteDuration);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SongNote other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(NoteType, NoteDuration);

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(SongNote)} note_type={NoteType.Name} note_duration={NoteDuration.Name}";

    /// <summary>Compares two notes by note type and duration.</summary>
    public static bool operator ==(SongNote left, SongNote right) => left.Equals(right);

    /// <summary>Compares two notes by note type and duration.</summary>
    public static bool operator !=(SongNote left, SongNote right) => !left.Equals(right);
}
