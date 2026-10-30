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
using System.Collections.Generic;

namespace Anki.Cozmo.Sdk;

/// <summary>Predefined colours and lookup helpers for the colour catalogue.</summary>
/// <remarks>
/// The catalogue contains green, red, blue, white and off. <see cref="Off"/> is a name-only
/// colour whose packed integer value is zero.
/// </remarks>
public static class Colors
{
    /// <summary>The colour that represents no light (LEDs off).</summary>
    public static Color Off { get; } = Color.FromName("off"); // lights.py:67.

    /// <summary>The green colour.</summary>
    public static Color Green { get; } = Color.FromIntColor(0x00FF00FF, "green"); // lights.py:55.

    /// <summary>The red colour.</summary>
    public static Color Red { get; } = Color.FromIntColor(0xFF0000FF, "red"); // lights.py:58.

    /// <summary>The blue colour.</summary>
    public static Color Blue { get; } = Color.FromIntColor(0x0000FFFF, "blue"); // lights.py:61.

    /// <summary>The white colour.</summary>
    public static Color White { get; } = Color.FromIntColor(0xFFFFFFFF, "white"); // lights.py:64.

    /// <summary>Every catalogue colour in declaration order.</summary>
    public static IReadOnlyList<Color> All { get; } = Array.AsReadOnly(new[] { Green, Red, Blue, White, Off });

    private static readonly Dictionary<string, Color> Lookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["green"] = Green,
        ["red"] = Red,
        ["blue"] = Blue,
        ["white"] = White,
        ["off"] = Off,
    };

    /// <summary>Resolves a named colour from the catalogue.</summary>
    /// <param name="name">Colour name; matching ignores case.</param>
    /// <returns>The named colour.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">The name is not in the catalogue.</exception>
    public static Color Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Lookup.TryGetValue(name, out Color color))
        {
            return color;
        }

        throw new KeyNotFoundException($"'{name}' is not a named colour.");
    }

    /// <summary>Tries to resolve a named colour from the catalogue.</summary>
    /// <param name="name">Colour name; matching ignores case. May be <see langword="null"/>.</param>
    /// <param name="color">
    /// When this method returns <see langword="true"/>, the named colour; otherwise
    /// <see langword="default"/>.
    /// </param>
    /// <returns><see langword="true"/> when the name is in the catalogue.</returns>
    public static bool TryGet(string? name, out Color color)
    {
        if (name is not null && Lookup.TryGetValue(name, out color))
        {
            return true;
        }

        color = default;
        return false;
    }
}
