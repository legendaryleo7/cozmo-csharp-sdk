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
using System.Runtime.CompilerServices;

namespace Anki.Cozmo.Sdk;

/// <summary>The settings of one light, as used by Cozmo's backpack and LightCubes.</summary>
/// <remarks>
/// <para>
/// A light has a colour for its "on" state and a colour for its "off" state, plus the period in
/// milliseconds spent in each state and the periods used to transition between them. As in
/// Python, both colours may be the same (a steady light) or either may be
/// <see cref="Colors.Off"/>.
/// </para>
/// <para>
/// Python's constructor assigns the six values directly, so it accepts zero periods; the public
/// constructor here does the same. The property setters enforce the Python setter rule
/// <c>0 &lt; ms &lt; 2^32</c> and throw <see cref="ArgumentOutOfRangeException"/> for zero.
/// Because a <see cref="uint"/> cannot hold 2^32 or more, zero is the only rejected value.
/// </para>
/// </remarks>
public sealed class Light
{
    /// <summary>Default period the light stays "on", in milliseconds.</summary>
    /// <remarks>Python <c>on_period_ms=250</c> (lights.py:77).</remarks>
    public const uint DefaultOnPeriodMs = 250; // lights.py:77.

    /// <summary>Default period the light stays "off", in milliseconds.</summary>
    /// <remarks>Python <c>off_period_ms=0</c> (lights.py:78).</remarks>
    public const uint DefaultOffPeriodMs = 0; // lights.py:78.

    /// <summary>Default period used to transition to the "on" colour, in milliseconds.</summary>
    /// <remarks>Python <c>transition_on_period_ms=0</c> (lights.py:78).</remarks>
    public const uint DefaultTransitionOnPeriodMs = 0; // lights.py:78.

    /// <summary>Default period used to transition to the "off" colour, in milliseconds.</summary>
    /// <remarks>Python <c>transition_off_period_ms=0</c> (lights.py:78).</remarks>
    public const uint DefaultTransitionOffPeriodMs = 0; // lights.py:78.

    /// <summary>Default period used by <see cref="Flash"/> for the "on" state, in milliseconds.</summary>
    /// <remarks>Python <c>flash(on_period_ms=250)</c> (lights.py:152).</remarks>
    public const uint DefaultFlashOnPeriodMs = 250; // lights.py:152.

    /// <summary>Default period used by <see cref="Flash"/> for the "off" state, in milliseconds.</summary>
    /// <remarks>Python <c>flash(off_period_ms=250)</c> (lights.py:152).</remarks>
    public const uint DefaultFlashOffPeriodMs = 250; // lights.py:152.

    private Color _onColor;
    private Color _offColor;
    private uint _onPeriodMs;
    private uint _offPeriodMs;
    private uint _transitionOnPeriodMs;
    private uint _transitionOffPeriodMs;

    /// <summary>Creates a light with the Python SDK's default settings.</summary>
    /// <param name="onColor">
    /// Colour shown while the light is on; <see langword="null"/> selects <see cref="Colors.Off"/>.
    /// </param>
    /// <param name="offColor">
    /// Colour shown while the light is off; <see langword="null"/> selects <see cref="Colors.Off"/>.
    /// </param>
    /// <param name="onPeriodMs">Milliseconds the light stays on in each cycle.</param>
    /// <param name="offPeriodMs">Milliseconds the light stays off in each cycle.</param>
    /// <param name="transitionOnPeriodMs">Milliseconds taken to transition to the on colour.</param>
    /// <param name="transitionOffPeriodMs">Milliseconds taken to transition to the off colour.</param>
    /// <remarks>
    /// Mirrors Python <c>Light(...)</c> (lights.py:77-84). Like the Python constructor, this
    /// constructor does not validate the periods; the setters do.
    /// </remarks>
    public Light(
        Color? onColor = null,
        Color? offColor = null,
        uint onPeriodMs = DefaultOnPeriodMs,
        uint offPeriodMs = DefaultOffPeriodMs,
        uint transitionOnPeriodMs = DefaultTransitionOnPeriodMs,
        uint transitionOffPeriodMs = DefaultTransitionOffPeriodMs)
    {
        _onColor = onColor ?? Colors.Off;
        _offColor = offColor ?? Colors.Off;
        _onPeriodMs = onPeriodMs;
        _offPeriodMs = offPeriodMs;
        _transitionOnPeriodMs = transitionOnPeriodMs;
        _transitionOffPeriodMs = transitionOffPeriodMs;
    }

    private Light(Light source)
    {
        _onColor = source._onColor;
        _offColor = source._offColor;
        _onPeriodMs = source._onPeriodMs;
        _offPeriodMs = source._offPeriodMs;
        _transitionOnPeriodMs = source._transitionOnPeriodMs;
        _transitionOffPeriodMs = source._transitionOffPeriodMs;
    }

    /// <summary>The colour shown while the light is on.</summary>
    public Color OnColor
    {
        get => _onColor;
        set => _onColor = value;
    }

    /// <summary>The colour shown while the light is off.</summary>
    public Color OffColor
    {
        get => _offColor;
        set => _offColor = value;
    }

    /// <summary>Milliseconds the light stays on in each cycle.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint OnPeriodMs
    {
        get => _onPeriodMs;
        set
        {
            ValidatePeriod(value);
            _onPeriodMs = value;
        }
    }

    /// <summary>Milliseconds the light stays off in each cycle.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint OffPeriodMs
    {
        get => _offPeriodMs;
        set
        {
            ValidatePeriod(value);
            _offPeriodMs = value;
        }
    }

    /// <summary>Milliseconds taken to transition the light to the on colour.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint TransitionOnPeriodMs
    {
        get => _transitionOnPeriodMs;
        set
        {
            ValidatePeriod(value);
            _transitionOnPeriodMs = value;
        }
    }

    /// <summary>Milliseconds taken to transition the light to the off colour.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint TransitionOffPeriodMs
    {
        get => _transitionOffPeriodMs;
        set
        {
            ValidatePeriod(value);
            _transitionOffPeriodMs = value;
        }
    }

    /// <summary>Creates a flashing copy of this light.</summary>
    /// <param name="onPeriodMs">Milliseconds the light stays on in each cycle.</param>
    /// <param name="offPeriodMs">Milliseconds the light stays off in each cycle.</param>
    /// <param name="offColor">
    /// Colour to flash to for the off state; <see langword="null"/> selects <see cref="Colors.Off"/>.
    /// </param>
    /// <returns>
    /// A new light with this light's on colour and transition periods, the supplied on/off
    /// periods, and the supplied off colour. This instance is not modified.
    /// </returns>
    /// <remarks>
    /// Port of <c>Light.flash</c> (lights.py:152-166). The Python implementation applies the
    /// property setters, so the same period validation applies here.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="onPeriodMs"/> or <paramref name="offPeriodMs"/> is zero.
    /// </exception>
    public Light Flash(
        uint onPeriodMs = DefaultFlashOnPeriodMs,
        uint offPeriodMs = DefaultFlashOffPeriodMs,
        Color? offColor = null)
    {
        var flasher = new Light(this);
        flasher.OnPeriodMs = onPeriodMs;
        flasher.OffPeriodMs = offPeriodMs;
        flasher.OffColor = offColor ?? Colors.Off;
        return flasher;
    }

    private static void ValidatePeriod(
        uint value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        // Python rejects values outside 0 < ms < 2**32 in the setters (lights.py:113-150).
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Invalid value.");
        }
    }
}
