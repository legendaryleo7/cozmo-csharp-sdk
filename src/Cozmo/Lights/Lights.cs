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
using Anki.Cozmo.ExternalInterface;

namespace Anki.Cozmo.Sdk;

/// <summary>The predefined lights and the helper that writes a light into CLAD LED messages.</summary>
/// <remarks>
/// Each predefined light is steady: its on and off colours are the same, so no flashing is
/// visible.
/// </remarks>
public static class Lights
{
    /// <summary>A steady green LED light.</summary>
    public static Light Green { get; } = new(Colors.Green, Colors.Green); // lights.py:183.

    /// <summary>A steady red LED light.</summary>
    public static Light Red { get; } = new(Colors.Red, Colors.Red); // lights.py:186.

    /// <summary>A steady blue LED light.</summary>
    public static Light Blue { get; } = new(Colors.Blue, Colors.Blue); // lights.py:189.

    /// <summary>A steady white LED light.</summary>
    public static Light White { get; } = new(Colors.White, Colors.White); // lights.py:192.

    /// <summary>A steady, non-illuminated LED light.</summary>
    public static Light Off { get; } = new(Colors.Off, Colors.Off); // lights.py:195.

    /// <summary>Writes a light into one slot of a cube LED message.</summary>
    /// <param name="message">The <c>SetAllActiveObjectLEDs</c> message to fill.</param>
    /// <param name="index">Slot to write, 0-3 for the four cube lights.</param>
    /// <param name="light">The light settings to copy.</param>
    /// <remarks>
    /// <para>
    /// The message stores 32-bit RGBA colours and millisecond periods. The generated
    /// <c>Anki.Cozmo.LightState</c> type represents engine-reported object state and uses a
    /// different colour and timing format.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="light"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the message array.</exception>
    /// <exception cref="ArgumentException">A message array is <see langword="null"/>.</exception>
    public static void SetLight(SetAllActiveObjectLEDs message, int index, Light light)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(light);

        // The six fields copied by Python's _set_light (lights.py:172-177).
        WriteSlot(message.onColor, index, light.OnColor.IntColor, nameof(SetAllActiveObjectLEDs), nameof(message.onColor));
        WriteSlot(message.offColor, index, light.OffColor.IntColor, nameof(SetAllActiveObjectLEDs), nameof(message.offColor));
        WriteSlot(message.onPeriod_ms, index, light.OnPeriodMs, nameof(SetAllActiveObjectLEDs), nameof(message.onPeriod_ms));
        WriteSlot(message.offPeriod_ms, index, light.OffPeriodMs, nameof(SetAllActiveObjectLEDs), nameof(message.offPeriod_ms));
        WriteSlot(message.transitionOnPeriod_ms, index, light.TransitionOnPeriodMs, nameof(SetAllActiveObjectLEDs), nameof(message.transitionOnPeriod_ms));
        WriteSlot(message.transitionOffPeriod_ms, index, light.TransitionOffPeriodMs, nameof(SetAllActiveObjectLEDs), nameof(message.transitionOffPeriod_ms));
    }

    /// <summary>Writes a light into one slot of a backpack LED message.</summary>
    /// <param name="message">The <c>SetBackpackLEDs</c> message to fill.</param>
    /// <param name="index">Slot to write, 0-4 for the five backpack lights.</param>
    /// <param name="light">The light settings to copy.</param>
    /// <remarks>
    /// <para>
    /// See <see cref="SetLight(SetAllActiveObjectLEDs, int, Light)"/> for why the generated
    /// <c>Anki.Cozmo.LightState</c> type is not the target of this helper.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="light"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the message array.</exception>
    /// <exception cref="ArgumentException">A message array is <see langword="null"/>.</exception>
    public static void SetLight(SetBackpackLEDs message, int index, Light light)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(light);

        // The six fields copied by Python's _set_light (lights.py:172-177).
        WriteSlot(message.onColor, index, light.OnColor.IntColor, nameof(SetBackpackLEDs), nameof(message.onColor));
        WriteSlot(message.offColor, index, light.OffColor.IntColor, nameof(SetBackpackLEDs), nameof(message.offColor));
        WriteSlot(message.onPeriod_ms, index, light.OnPeriodMs, nameof(SetBackpackLEDs), nameof(message.onPeriod_ms));
        WriteSlot(message.offPeriod_ms, index, light.OffPeriodMs, nameof(SetBackpackLEDs), nameof(message.offPeriod_ms));
        WriteSlot(message.transitionOnPeriod_ms, index, light.TransitionOnPeriodMs, nameof(SetBackpackLEDs), nameof(message.transitionOnPeriod_ms));
        WriteSlot(message.transitionOffPeriod_ms, index, light.TransitionOffPeriodMs, nameof(SetBackpackLEDs), nameof(message.transitionOffPeriod_ms));
    }

    private static void WriteSlot<T>(T[]? values, int index, T value, string messageType, string fieldName)
    {
        if (values is null)
        {
            throw new ArgumentException($"{messageType}.{fieldName} is null.", "message");
        }

        // CLAD fixed arrays are indexed 0..Length-1; Python raises IndexError for the same cases.
        if ((uint)index >= (uint)values.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"{messageType}.{fieldName} has {values.Length} element(s).");
        }

        values[index] = value;
    }
}
