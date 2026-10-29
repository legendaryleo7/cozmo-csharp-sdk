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

using System;

namespace Anki.Cozmo.Sdk;

/// <summary>An immutable colour used by a <see cref="Light"/>.</summary>
/// <remarks>
/// <para>
/// Python's <c>cozmo.lights.Color</c> packs a colour into a 32-bit integer ordered as
/// <c>red &lt;&lt; 24 | green &lt;&lt; 16 | blue &lt;&lt; 8 | alpha</c>. This type stores the same
/// integer in <see cref="IntColor"/> and exposes its bytes as <see cref="R"/>, <see cref="G"/>,
/// <see cref="B"/> and <see cref="A"/>.
/// </para>
/// <para>
/// As in Python, colours created from an integer or an RGB triple are fully opaque: the alpha
/// byte is forced to <see cref="OpaqueAlpha"/>. A colour created from a name only carries no
/// light value (<see cref="IntColor"/> is zero), which is how <see cref="Colors.Off"/>
/// represents an unlit LED.
/// </para>
/// <para>
/// Equality compares <see cref="IntColor"/> only; <see cref="Name"/> is a descriptive label and
/// does not participate. Python's <c>Color</c> has identity equality, so using a value type here
/// is a deliberate, documented difference.
/// </para>
/// </remarks>
public readonly struct Color : IEquatable<Color>
{
    /// <summary>Alpha byte forced for colours built from an integer or RGB components.</summary>
    public const byte OpaqueAlpha = 0xFF; // lights.py:44 forces alpha opaque for int/rgb colours.

    private readonly uint _intColor;
    private readonly string? _name;

    /// <summary>Initializes an opaque colour from a packed RGBA integer.</summary>
    /// <param name="intColor">
    /// Packed <c>R &lt;&lt; 24 | G &lt;&lt; 16 | B &lt;&lt; 8 | A</c> value.
    /// </param>
    /// <param name="name">Optional descriptive name; it does not affect the colour value.</param>
    /// <remarks>
    /// Mirrors Python <c>Color(int_color=...)</c>. The input is validated to be a 32-bit
    /// unsigned value by its type, and the alpha byte is replaced with
    /// <see cref="OpaqueAlpha"/> (lights.py:44).
    /// </remarks>
    public Color(uint intColor, string? name = null)
        : this(intColor, name, forceOpaque: true)
    {
    }

    private Color(uint intColor, string? name, bool forceOpaque)
    {
        _intColor = forceOpaque ? intColor | OpaqueAlpha : intColor;
        _name = name;
    }

    /// <summary>The packed RGBA integer value.</summary>
    /// <remarks>Equal to <c>R &lt;&lt; 24 | G &lt;&lt; 16 | B &lt;&lt; 8 | A</c> (lights.py:48-51).</remarks>
    public uint IntColor => _intColor;

    /// <summary>The optional descriptive name, or <see langword="null"/> when unnamed.</summary>
    public string? Name => _name;

    /// <summary>The red component (most significant byte).</summary>
    public byte R => (byte)(_intColor >> 24);

    /// <summary>The green component.</summary>
    public byte G => (byte)(_intColor >> 16);

    /// <summary>The blue component.</summary>
    public byte B => (byte)(_intColor >> 8);

    /// <summary>The alpha component (least significant byte); opaque for lit colours.</summary>
    public byte A => (byte)_intColor;

    /// <summary>Creates an opaque colour from a packed RGBA integer.</summary>
    /// <param name="intColor">Packed <c>R &lt;&lt; 24 | G &lt;&lt; 16 | B &lt;&lt; 8 | A</c> value.</param>
    /// <param name="name">Optional descriptive name.</param>
    /// <returns>The new colour.</returns>
    /// <remarks>The alpha byte is forced opaque (lights.py:43-44).</remarks>
    public static Color FromIntColor(uint intColor, string? name = null) => new(intColor, name);

    /// <summary>Creates an opaque colour from red, green and blue components.</summary>
    /// <param name="red">Red component, 0-255.</param>
    /// <param name="green">Green component, 0-255.</param>
    /// <param name="blue">Blue component, 0-255.</param>
    /// <param name="name">Optional descriptive name.</param>
    /// <returns>The new colour.</returns>
    /// <remarks>
    /// Mirrors Python <c>Color(rgb=(red, green, blue))</c>; the alpha byte is forced opaque
    /// (lights.py:45-46).
    /// </remarks>
    public static Color FromRgb(byte red, byte green, byte blue, string? name = null)
        // lights.py:46 packs (r << 24) | (g << 16) | (b << 8) | 0xff.
        => new(((uint)red << 24) | ((uint)green << 16) | ((uint)blue << 8), name);

    /// <summary>Creates a colour that carries a name but no light value.</summary>
    /// <param name="name">Descriptive name of the colour.</param>
    /// <returns>A colour whose <see cref="IntColor"/> is zero.</returns>
    /// <remarks>
    /// Mirrors Python <c>Color(name=...)</c> without <c>int_color</c> or <c>rgb</c>
    /// (lights.py:42-47). Use <see cref="Colors.Get"/> to resolve a catalogue name to its
    /// actual colour.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static Color FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new Color(0, name, forceOpaque: false);
    }

    /// <summary>Returns the red, green and blue components of the colour.</summary>
    /// <returns>A tuple holding the three 0-255 components.</returns>
    public (byte R, byte G, byte B) ToRgb() => (R, G, B);

    /// <inheritdoc />
    public bool Equals(Color other) => _intColor == other._intColor;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _intColor.GetHashCode();

    /// <summary>Compares two colours by their packed RGBA value.</summary>
    /// <param name="left">Left colour.</param>
    /// <param name="right">Right colour.</param>
    /// <returns><see langword="true"/> when both colours have the same <see cref="IntColor"/>.</returns>
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>Compares two colours by their packed RGBA value.</summary>
    /// <param name="left">Left colour.</param>
    /// <param name="right">Right colour.</param>
    /// <returns><see langword="true"/> when the colours have different <see cref="IntColor"/> values.</returns>
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    /// <summary>Returns the name when present, otherwise the packed value as <c>#RRGGBBAA</c>.</summary>
    /// <returns>A human-readable representation of the colour.</returns>
    public override string ToString() => _name ?? $"#{_intColor:X8}";
}
