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

/// <summary>Represents a distance.</summary>
/// <remarks>
/// <para>Mirrors <c>Distance</c> in <c>cozmo/util.py</c>.</para>
/// <para>
/// Python accepts <c>distance_mm</c> or <c>distance_inches</c> and raises <c>ValueError</c>
/// when neither is given. The C# port exposes one factory per unit instead; supplying both
/// units is a compile-time error.
/// </para>
/// </remarks>
public readonly struct Distance
{
    /// <summary>Creates a distance from a value in millimeters.</summary>
    /// <param name="millimeters">The distance in millimeters.</param>
    public Distance(double millimeters)
    {
        DistanceMillimeters = millimeters;
    }

    /// <summary>The distance in millimeters.</summary>
    public double DistanceMillimeters { get; }

    /// <summary>The distance in inches.</summary>
    public double DistanceInches => DistanceMillimeters / 25.4; // util.py:249

    /// <summary>Creates a distance from a value in millimeters.</summary>
    /// <param name="millimeters">The distance in millimeters.</param>
    /// <returns>A new <see cref="Distance"/>.</returns>
    public static Distance FromMillimeters(double millimeters) => new(millimeters);

    /// <summary>Creates a distance from a value in inches.</summary>
    /// <param name="inches">The distance in inches.</param>
    /// <returns>A new <see cref="Distance"/>.</returns>
    public static Distance FromInches(double inches) => new(inches * 25.4); // util.py:215

    /// <summary>Adds two distances.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the two distances.</returns>
    public static Distance operator +(Distance left, Distance right) =>
        new(left.DistanceMillimeters + right.DistanceMillimeters);

    /// <summary>Subtracts one distance from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The difference of the two distances.</returns>
    public static Distance operator -(Distance left, Distance right) =>
        new(left.DistanceMillimeters - right.DistanceMillimeters);

    /// <summary>Multiplies a distance by a scalar.</summary>
    /// <param name="left">The distance.</param>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>The scaled distance.</returns>
    public static Distance operator *(Distance left, double right) => new(left.DistanceMillimeters * right);

    /// <summary>Divides a distance by a scalar.</summary>
    /// <param name="left">The distance.</param>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>The scaled distance.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero, as Python raises <c>ZeroDivisionError</c>.</exception>
    public static Distance operator /(Distance left, double right)
    {
        if (right == 0.0)
        {
            throw new DivideByZeroException();
        }

        return new Distance(left.DistanceMillimeters / right);
    }

    /// <summary>Returns Python's <c>repr</c> representation of this distance.</summary>
    /// <returns>A string such as <c>&lt;Distance 25.40 mm (1.00 inches)&gt;</c>.</returns>
    public override string ToString() =>
        FormattableString.Invariant(
            $"<{nameof(Distance)} {DistanceMillimeters:F2} mm ({DistanceInches:F2} inches)>"); // util.py:219
}
