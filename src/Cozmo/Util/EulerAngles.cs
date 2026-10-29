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

/// <summary>The pitch, yaw and roll Euler components of a rotation.</summary>
/// <remarks>
/// Python's <c>Quaternion.euler_angles</c> returns a plain <c>(pitch, yaw, roll)</c> tuple
/// (util.py:764-797). This readonly struct preserves the same order and value semantics while
/// giving the components readable names.
/// </remarks>
public readonly struct EulerAngles : IEquatable<EulerAngles>
{
    /// <summary>Creates an Euler angle triple.</summary>
    /// <param name="pitch">Rotation about the x axis, in radians.</param>
    /// <param name="yaw">Rotation about the y axis, in radians.</param>
    /// <param name="roll">Rotation about the z axis, in radians.</param>
    public EulerAngles(double pitch, double yaw, double roll)
    {
        Pitch = pitch;
        Yaw = yaw;
        Roll = roll;
    }

    /// <summary>Rotation about the x axis, in radians.</summary>
    public double Pitch { get; }

    /// <summary>Rotation about the y axis, in radians.</summary>
    public double Yaw { get; }

    /// <summary>Rotation about the z axis, in radians.</summary>
    public double Roll { get; }

    /// <summary>Deconstructs the components in Python's tuple order.</summary>
    /// <param name="pitch">Rotation about the x axis, in radians.</param>
    /// <param name="yaw">Rotation about the y axis, in radians.</param>
    /// <param name="roll">Rotation about the z axis, in radians.</param>
    public void Deconstruct(out double pitch, out double yaw, out double roll)
    {
        pitch = Pitch;
        yaw = Yaw;
        roll = Roll;
    }

    /// <summary>Determines whether two Euler angle triples are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when all three components are equal.</returns>
    public static bool operator ==(EulerAngles left, EulerAngles right) => left.Equals(right);

    /// <summary>Determines whether two Euler angle triples are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when any component differs.</returns>
    public static bool operator !=(EulerAngles left, EulerAngles right) => !left.Equals(right);

    /// <summary>Determines whether this triple equals another triple.</summary>
    /// <param name="other">The triple to compare against.</param>
    /// <returns><see langword="true"/> when all three components are equal.</returns>
    public bool Equals(EulerAngles other) =>
        Pitch == other.Pitch && Yaw == other.Yaw && Roll == other.Roll;

    /// <summary>Determines whether this triple equals another object.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="EulerAngles"/>.</returns>
    public override bool Equals(object? obj) => obj is EulerAngles other && Equals(other);

    /// <summary>Returns a hash code for this triple.</summary>
    /// <returns>A hash code combining all three components.</returns>
    public override int GetHashCode() => HashCode.Combine(Pitch, Yaw, Roll);

    /// <summary>Returns a diagnostic representation of this triple.</summary>
    /// <returns>A string in Python's tuple order.</returns>
    public override string ToString() =>
        FormattableString.Invariant($"({Pitch}, {Yaw}, {Roll})");
}
