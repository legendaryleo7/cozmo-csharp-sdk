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

/// <summary>Represents an angle.</summary>
/// <remarks>
/// <para>Mirrors <c>Angle</c> in <c>cozmo/util.py</c>.</para>
/// <para>
/// Python accepts either a <c>radians</c> or a <c>degrees</c> keyword argument and raises
/// <c>ValueError</c> when neither is given. The C# port makes the unit explicit through
/// <see cref="FromDegrees"/> and <see cref="FromRadians"/>; the single-argument constructor
/// mirrors Python's positional <c>radians</c> argument. Supplying both units is therefore a
/// compile-time error rather than a runtime exception.
/// </para>
/// <para>
/// Python raises <c>TypeError</c> when an angle is compared with or added to a non-angle.
/// C# overload resolution reports those mistakes at compile time; <see cref="Equals(object)"/>
/// returns <see langword="false"/> for other types instead of throwing.
/// </para>
/// </remarks>
public readonly struct Angle : IEquatable<Angle>
{
    /// <summary>Creates an angle from a value in radians.</summary>
    /// <param name="radians">The angle in radians.</param>
    public Angle(double radians)
    {
        Radians = radians;
    }

    /// <summary>The angle in radians.</summary>
    public double Radians { get; }

    /// <summary>The angle in degrees.</summary>
    public double Degrees => Radians / Math.PI * 180.0; // util.py:170

    /// <summary>The absolute value of the angle.</summary>
    /// <remarks>Mirrors Python's <c>Angle.abs_value</c> (util.py:173).</remarks>
    public Angle AbsValue => new(Math.Abs(Radians));

    /// <summary>Creates an angle from a value in degrees.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>A new <see cref="Angle"/>.</returns>
    public static Angle FromDegrees(double degrees) => new(degrees * Math.PI / 180.0); // util.py:113

    /// <summary>Creates an angle from a value in radians.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>A new <see cref="Angle"/>.</returns>
    public static Angle FromRadians(double radians) => new(radians);

    /// <summary>Adds two angles.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the two angles.</returns>
    public static Angle operator +(Angle left, Angle right) => new(left.Radians + right.Radians);

    /// <summary>Subtracts one angle from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The difference of the two angles.</returns>
    public static Angle operator -(Angle left, Angle right) => new(left.Radians - right.Radians);

    /// <summary>Multiplies an angle by a scalar.</summary>
    /// <param name="left">The angle.</param>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>The scaled angle.</returns>
    public static Angle operator *(Angle left, double right) => new(left.Radians * right);

    /// <summary>Divides an angle by a scalar.</summary>
    /// <param name="left">The angle.</param>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>The scaled angle.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero, as Python raises <c>ZeroDivisionError</c>.</exception>
    public static Angle operator /(Angle left, double right)
    {
        if (right == 0.0)
        {
            throw new DivideByZeroException();
        }

        return new Angle(left.Radians / right);
    }

    /// <summary>Determines whether two angles are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both angles have the same radian value.</returns>
    public static bool operator ==(Angle left, Angle right) => left.Radians == right.Radians;

    /// <summary>Determines whether two angles are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the radian values differ.</returns>
    public static bool operator !=(Angle left, Angle right) => left.Radians != right.Radians;

    /// <summary>Determines whether one angle is greater than another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is greater.</returns>
    public static bool operator >(Angle left, Angle right) => left.Radians > right.Radians;

    /// <summary>Determines whether one angle is less than another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is smaller.</returns>
    public static bool operator <(Angle left, Angle right) => left.Radians < right.Radians;

    /// <summary>Determines whether one angle is greater than or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is greater than or equal.</returns>
    public static bool operator >=(Angle left, Angle right) => left.Radians >= right.Radians;

    /// <summary>Determines whether one angle is less than or equal to another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is less than or equal.</returns>
    public static bool operator <=(Angle left, Angle right) => left.Radians <= right.Radians;

    /// <summary>Determines whether this angle equals another angle.</summary>
    /// <param name="other">The angle to compare against.</param>
    /// <returns><see langword="true"/> when the radian values are equal.</returns>
    public bool Equals(Angle other) => Radians == other.Radians;

    /// <summary>Determines whether this angle equals another object.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="Angle"/>.</returns>
    public override bool Equals(object? obj) => obj is Angle other && Equals(other);

    /// <summary>Returns a hash code for this angle.</summary>
    /// <returns>A hash code based on the radian value.</returns>
    public override int GetHashCode() => Radians.GetHashCode();

    /// <summary>Returns Python's <c>repr</c> representation of this angle.</summary>
    /// <returns>A string such as <c>&lt;Angle 0.52 radians (30.00 degrees)&gt;</c>.</returns>
    public override string ToString() =>
        FormattableString.Invariant($"<{nameof(Angle)} {Radians:F2} radians ({Degrees:F2} degrees)>"); // util.py:117
}
