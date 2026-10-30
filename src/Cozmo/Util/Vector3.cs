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

/// <summary>Represents a 3D vector whose type and units are unspecified.</summary>
/// <remarks>
/// Components are exposed through read-only properties and can be changed with
/// <see cref="SetTo"/>. <see cref="Position"/> derives from this type.
/// </remarks>
public class Vector3
{
    private double _x;
    private double _y;
    private double _z;

    /// <summary>Creates a vector from its components.</summary>
    /// <param name="x">The x component.</param>
    /// <param name="y">The y component.</param>
    /// <param name="z">The z component.</param>
    public Vector3(double x, double y, double z)
    {
        _x = x;
        _y = y;
        _z = z;
    }

    /// <summary>The x component.</summary>
    public double X => _x;

    /// <summary>The y component.</summary>
    public double Y => _y;

    /// <summary>The z component.</summary>
    public double Z => _z;

    /// <summary>The X, Y and Z elements of the vector (x, y, z).</summary>
    public (double X, double Y, double Z) Xyz => (_x, _y, _z);

    /// <summary>Copies the x, y and z components of the given vector into this vector.</summary>
    /// <param name="rhs">The source vector to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rhs"/> is <see langword="null"/>.</exception>
    public void SetTo(Vector3 rhs)
    {
        ArgumentNullException.ThrowIfNull(rhs);
        _x = rhs.X;
        _y = rhs.Y;
        _z = rhs.Z;
    }

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The component-wise sum.</returns>
    /// <exception cref="ArgumentNullException">An operand is <see langword="null"/>.</exception>
    public static Vector3 operator +(Vector3 left, Vector3 right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>Subtracts one vector from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The component-wise difference.</returns>
    /// <exception cref="ArgumentNullException">An operand is <see langword="null"/>.</exception>
    public static Vector3 operator -(Vector3 left, Vector3 right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    /// <summary>Multiplies a vector by a scalar.</summary>
    /// <param name="left">The vector.</param>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>The scaled vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> is <see langword="null"/>.</exception>
    public static Vector3 operator *(Vector3 left, double right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return new Vector3(left.X * right, left.Y * right, left.Z * right);
    }

    /// <summary>Divides a vector by a scalar.</summary>
    /// <param name="left">The vector.</param>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>The scaled vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> is <see langword="null"/>.</exception>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    public static Vector3 operator /(Vector3 left, double right)
    {
        ArgumentNullException.ThrowIfNull(left);
        if (right == 0.0)
        {
            throw new DivideByZeroException();
        }

        return new Vector3(left.X / right, left.Y / right, left.Z / right);
    }

    /// <summary>Returns a diagnostic string representation of this vector.</summary>
    /// <returns>A string such as <c>&lt;Vector3 x: 1.50 y: -2.00 z: 3.25&gt;</c>.</returns>
    public override string ToString() => // util.py:949-950
        FormattableString.Invariant($"<{GetType().Name} x: {X:F2} y: {Y:F2} z: {Z:F2}>");
}
