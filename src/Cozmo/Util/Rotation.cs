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

/// <summary>An alias for <see cref="Quaternion"/>.</summary>
/// <remarks>
/// <para>
/// Python defines <c>Rotation(Quaternion)</c> as an empty subclass (util.py:800-802), so a
/// <c>Rotation</c> supports exactly the same operations as a <c>Quaternion</c>. C# value types
/// cannot inherit, so this readonly struct wraps a <see cref="Quaternion"/> and offers
/// implicit conversions in both directions. Return values that are <see cref="Quaternion"/>
/// in Python (for example <c>rotation_quaternion</c>) can therefore still be assigned to a
/// <see cref="Rotation"/> variable.
/// </para>
/// <para>
/// As in Python, arithmetic operators on <see cref="Rotation"/> return a scaled z-angle
/// rotation; the C# port returns <see cref="Rotation"/> where Python returns a
/// <see cref="Quaternion"/> instance. The implicit conversion makes the two interchangeable.
/// </para>
/// </remarks>
public readonly struct Rotation
{
    private readonly Quaternion _quaternion;

    /// <summary>Creates a rotation from a quaternion.</summary>
    /// <param name="quaternion">The underlying quaternion.</param>
    public Rotation(Quaternion quaternion)
    {
        _quaternion = quaternion;
    }

    /// <summary>Creates a rotation from its four quaternion components.</summary>
    /// <param name="q0">The q0 (w) component.</param>
    /// <param name="q1">The q1 (i) component.</param>
    /// <param name="q2">The q2 (j) component.</param>
    /// <param name="q3">The q3 (k) component.</param>
    public Rotation(double q0, double q1, double q2, double q3)
        : this(new Quaternion(q0, q1, q2, q3))
    {
    }

    /// <summary>The underlying quaternion.</summary>
    public Quaternion Value => _quaternion;

    /// <summary>The q0 (w) value of the quaternion.</summary>
    public double Q0 => _quaternion.Q0;

    /// <summary>The q1 (i) value of the quaternion.</summary>
    public double Q1 => _quaternion.Q1;

    /// <summary>The q2 (j) value of the quaternion.</summary>
    public double Q2 => _quaternion.Q2;

    /// <summary>The q3 (k) value of the quaternion.</summary>
    public double Q3 => _quaternion.Q3;

    /// <summary>Contains all four elements of the quaternion (q0, q1, q2, q3).</summary>
    public (double Q0, double Q1, double Q2, double Q3) Components => _quaternion.Components;

    /// <summary>The z Euler component of the object's rotation.</summary>
    public Angle AngleZ => _quaternion.AngleZ;

    /// <summary>The pitch, yaw and roll Euler components of the object's rotation.</summary>
    public EulerAngles EulerAngles => _quaternion.EulerAngles;

    /// <summary>Creates a rotation from a quaternion.</summary>
    /// <param name="q0">The q0 (w) component.</param>
    /// <param name="q1">The q1 (i) component.</param>
    /// <param name="q2">The q2 (j) component.</param>
    /// <param name="q3">The q3 (k) component.</param>
    /// <returns>A new <see cref="Rotation"/>.</returns>
    public static Rotation FromQuaternion(double q0, double q1, double q2, double q3) =>
        new(new Quaternion(q0, q1, q2, q3));

    /// <summary>Creates a rotation from an angle about the z axis.</summary>
    /// <param name="angleZ">The angle about the z axis.</param>
    /// <returns>A new <see cref="Rotation"/>.</returns>
    public static Rotation FromZAngle(Angle angleZ) => new(Quaternion.FromZAngle(angleZ));

    /// <summary>Converts this rotation to a 4x4 matrix representing its rotation.</summary>
    /// <param name="posX">The x component for the position.</param>
    /// <param name="posY">The y component for the position.</param>
    /// <param name="posZ">The z component for the position.</param>
    /// <returns>A matrix representing this rotation with the provided position.</returns>
    public Matrix44 ToMatrix(double posX = 0.0, double posY = 0.0, double posZ = 0.0) =>
        _quaternion.ToMatrix(posX, posY, posZ);

    /// <summary>Adds two rotations as rotations about the z axis.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A rotation whose z angle is the sum of the operands' z angles.</returns>
    public static Rotation operator +(Rotation left, Rotation right) =>
        new((Quaternion)left + (Quaternion)right);

    /// <summary>Subtracts two rotations as rotations about the z axis.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A rotation whose z angle is the difference of the operands' z angles.</returns>
    public static Rotation operator -(Rotation left, Rotation right) =>
        new((Quaternion)left - (Quaternion)right);

    /// <summary>Multiplies a rotation's z angle by a scalar.</summary>
    /// <param name="left">The rotation.</param>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>A rotation with the scaled z angle.</returns>
    public static Rotation operator *(Rotation left, double right) => new((Quaternion)left * right);

    /// <summary>Divides a rotation's z angle by a scalar.</summary>
    /// <param name="left">The rotation.</param>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>A rotation with the scaled z angle.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero, as Python raises <c>ZeroDivisionError</c>.</exception>
    public static Rotation operator /(Rotation left, double right) => new((Quaternion)left / right);

    /// <summary>Implicitly converts a rotation to its underlying quaternion.</summary>
    /// <param name="rotation">The rotation to convert.</param>
    /// <returns>The underlying quaternion.</returns>
    public static implicit operator Quaternion(Rotation rotation) => rotation._quaternion;

    /// <summary>Implicitly treats a quaternion as a rotation.</summary>
    /// <param name="quaternion">The quaternion to wrap.</param>
    /// <returns>A rotation backed by <paramref name="quaternion"/>.</returns>
    public static implicit operator Rotation(Quaternion quaternion) => new(quaternion);

    /// <summary>Returns Python's <c>repr</c> representation of this rotation.</summary>
    /// <returns>A string such as <c>&lt;Rotation q0: 1.00 q1: 0.00 q2: 0.00 q3: 0.00 ...&gt;</c>.</returns>
    public override string ToString() => _quaternion.ToStringWithTypeName(nameof(Rotation));
}
