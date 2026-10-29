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

/// <summary>Represents a speed measured in millimeters per second.</summary>
/// <remarks>Mirrors <c>Speed</c> in <c>cozmo/util.py</c>.</remarks>
public readonly struct Speed
{
    /// <summary>Creates a speed from a value in millimeters per second.</summary>
    /// <param name="millimetersPerSecond">The speed in millimeters per second.</param>
    public Speed(double millimetersPerSecond)
    {
        SpeedMmps = millimetersPerSecond;
    }

    /// <summary>The speed in millimeters per second (mmps).</summary>
    public double SpeedMmps { get; }

    /// <summary>Creates a speed from a value in millimeters per second.</summary>
    /// <param name="millimetersPerSecond">The speed in millimeters per second.</param>
    /// <returns>A new <see cref="Speed"/>.</returns>
    public static Speed FromMillimetersPerSecond(double millimetersPerSecond) => new(millimetersPerSecond);

    /// <summary>Adds two speeds.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the two speeds.</returns>
    public static Speed operator +(Speed left, Speed right) => new(left.SpeedMmps + right.SpeedMmps);

    /// <summary>Subtracts one speed from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The difference of the two speeds.</returns>
    public static Speed operator -(Speed left, Speed right) => new(left.SpeedMmps - right.SpeedMmps);

    /// <summary>Multiplies a speed by a scalar.</summary>
    /// <param name="left">The speed.</param>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>The scaled speed.</returns>
    public static Speed operator *(Speed left, double right) => new(left.SpeedMmps * right);

    /// <summary>Divides a speed by a scalar.</summary>
    /// <param name="left">The speed.</param>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>The scaled speed.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero, as Python raises <c>ZeroDivisionError</c>.</exception>
    public static Speed operator /(Speed left, double right)
    {
        if (right == 0.0)
        {
            throw new DivideByZeroException();
        }

        return new Speed(left.SpeedMmps / right);
    }

    /// <summary>Returns Python's <c>repr</c> representation of this speed.</summary>
    /// <returns>A string such as <c>&lt;Speed 10.00 mmps&gt;</c>.</returns>
    public override string ToString() =>
        FormattableString.Invariant($"<{nameof(Speed)} {SpeedMmps:F2} mmps>"); // util.py:283
}
