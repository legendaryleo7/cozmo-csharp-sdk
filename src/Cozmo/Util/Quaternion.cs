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

/// <summary>Represents the rotation of an object in the world as a quaternion.</summary>
/// <remarks>
/// <para>Mirrors <c>Quaternion</c> in <c>cozmo/util.py</c>.</para>
/// <para>
/// Python requires either all four quaternion components or an <c>angle_z</c> and raises
/// <c>ValueError</c> for any other combination. The C# port exposes two constructors
/// (<see cref="Quaternion(double, double, double, double)"/> and
/// <see cref="FromZAngle"/>) so an invalid combination cannot be expressed. Python's
/// <c>Rotation</c> alias is represented by the separate <see cref="Rotation"/> struct.
/// </para>
/// </remarks>
public readonly struct Quaternion
{
    /// <summary>Creates a quaternion from its four components.</summary>
    /// <param name="q0">The q0 (w) component.</param>
    /// <param name="q1">The q1 (i) component.</param>
    /// <param name="q2">The q2 (j) component.</param>
    /// <param name="q3">The q3 (k) component.</param>
    public Quaternion(double q0, double q1, double q2, double q3)
    {
        Q0 = q0;
        Q1 = q1;
        Q2 = q2;
        Q3 = q3;
    }

    /// <summary>The q0 (w) value of the quaternion.</summary>
    public double Q0 { get; }

    /// <summary>The q1 (i) value of the quaternion.</summary>
    public double Q1 { get; }

    /// <summary>The q2 (j) value of the quaternion.</summary>
    public double Q2 { get; }

    /// <summary>The q3 (k) value of the quaternion.</summary>
    public double Q3 { get; }

    /// <summary>Contains all four elements of the quaternion (q0, q1, q2, q3).</summary>
    /// <remarks>Mirrors Python's <c>q0_q1_q2_q3</c> property (util.py:750).</remarks>
    public (double Q0, double Q1, double Q2, double Q3) Components => (Q0, Q1, Q2, Q3);

    /// <summary>The z Euler component of the object's rotation.</summary>
    /// <remarks>Mirrors Python's <c>Quaternion.angle_z</c> (util.py:755).</remarks>
    public Angle AngleZ =>
        Angle.FromRadians(
            Math.Atan2(
                2.0 * ((Q1 * Q2) + (Q0 * Q3)),
                1.0 - (2.0 * ((Q2 * Q2) + (Q3 * Q3))))); // util.py:761

    /// <summary>The pitch, yaw and roll Euler components of the object's rotation.</summary>
    /// <remarks>
    /// Mirrors Python's <c>Quaternion.euler_angles</c> (util.py:764). Rotations are
    /// interpreted in the order Z, Y, X and the near-gimbal-lock branch is reproduced.
    /// </remarks>
    public EulerAngles EulerAngles
    {
        get
        {
            Matrix44 matrix = ToMatrix();

            // Normalize the magnitudes of cos(roll)*sin(pitch) (m12) and
            // cos(roll)*cos(pitch) (m22) to isolate cos(roll) against -sin(roll) (m02).
            double absoluteCosRoll = Math.Sqrt((matrix.M12 * matrix.M12) + (matrix.M22 * matrix.M22));
            bool nearGimbalLock = absoluteCosRoll < 1e-6; // util.py:780

            double pitch;
            double yaw;
            double roll;
            if (!nearGimbalLock)
            {
                pitch = Math.Atan2(matrix.M22, matrix.M12);
                yaw = Math.Atan2(matrix.M00, matrix.M01);
                roll = Math.Atan2(absoluteCosRoll, -matrix.M02);
            }
            else
            {
                pitch = Math.Atan2(matrix.M11, -matrix.M21);
                yaw = 0.0;
                roll = Math.Atan2(absoluteCosRoll, -matrix.M02);
            }

            // Adjust roll to be consistent with how the device is oriented.
            roll = (Math.PI * 0.5) - roll; // util.py:793
            if (roll > Math.PI)
            {
                roll -= Math.PI * 2.0;
            }

            return new EulerAngles(pitch, yaw, roll);
        }
    }

    /// <summary>Creates a rotation from an angle about the z axis.</summary>
    /// <param name="angleZ">The angle about the z axis.</param>
    /// <returns>A new <see cref="Quaternion"/>.</returns>
    public static Quaternion FromZAngle(Angle angleZ)
    {
        (double q0, double q1, double q2, double q3) = Util.AngleZToQuaternion(angleZ);
        return new Quaternion(q0, q1, q2, q3);
    }

    /// <summary>Converts this quaternion to a 4x4 matrix representing its rotation.</summary>
    /// <param name="posX">The x component for the position.</param>
    /// <param name="posY">The y component for the position.</param>
    /// <param name="posZ">The z component for the position.</param>
    /// <returns>A matrix representing this rotation with the provided position.</returns>
    /// <remarks>Mirrors Python's <c>Quaternion.to_matrix</c> (util.py:662).</remarks>
    public Matrix44 ToMatrix(double posX = 0.0, double posY = 0.0, double posZ = 0.0)
    {
        // See https://en.wikipedia.org/wiki/Quaternions_and_spatial_rotation
        double q0q0 = Q0 * Q0;
        double q1q1 = Q1 * Q1;
        double q2q2 = Q2 * Q2;
        double q3q3 = Q3 * Q3;

        double q0x2 = Q0 * 2.0;
        double q0q1x2 = q0x2 * Q1;
        double q0q2x2 = q0x2 * Q2;
        double q0q3x2 = q0x2 * Q3;
        double q1x2 = Q1 * 2.0;
        double q1q2x2 = q1x2 * Q2;
        double q1q3x2 = q1x2 * Q3;
        double q2q3x2 = 2.0 * Q2 * Q3;

        double m00 = q0q0 + q1q1 - q2q2 - q3q3;
        double m01 = q1q2x2 + q0q3x2;
        double m02 = q1q3x2 - q0q2x2;

        double m10 = q1q2x2 - q0q3x2;
        double m11 = q0q0 - q1q1 + q2q2 - q3q3;
        double m12 = q0q1x2 + q2q3x2;

        double m20 = q0q2x2 + q1q3x2;
        double m21 = q2q3x2 - q0q1x2;
        double m22 = q0q0 - q1q1 - q2q2 + q3q3;

        return new Matrix44(
            m00, m10, m20, posX,
            m01, m11, m21, posY,
            m02, m12, m22, posZ,
            0.0, 0.0, 0.0, 1.0);
    }

    /// <summary>Adds two quaternions as rotations about the z axis.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A quaternion whose z angle is the sum of the operands' z angles.</returns>
    public static Quaternion operator +(Quaternion left, Quaternion right) =>
        FromZAngle(left.AngleZ + right.AngleZ); // util.py:709-712

    /// <summary>Subtracts two quaternions as rotations about the z axis.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A quaternion whose z angle is the difference of the operands' z angles.</returns>
    public static Quaternion operator -(Quaternion left, Quaternion right) =>
        FromZAngle(left.AngleZ - right.AngleZ); // util.py:714-717

    /// <summary>Multiplies a quaternion's z angle by a scalar.</summary>
    /// <param name="left">The quaternion.</param>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>A quaternion with the scaled z angle.</returns>
    public static Quaternion operator *(Quaternion left, double right) =>
        FromZAngle(left.AngleZ * right); // util.py:719-722

    /// <summary>Divides a quaternion's z angle by a scalar.</summary>
    /// <param name="left">The quaternion.</param>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>A quaternion with the scaled z angle.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero, as Python raises <c>ZeroDivisionError</c>.</exception>
    public static Quaternion operator /(Quaternion left, double right) =>
        FromZAngle(left.AngleZ / right); // util.py:724-727

    /// <summary>Returns Python's <c>repr</c> representation of this quaternion.</summary>
    /// <returns>A string such as <c>&lt;Quaternion q0: 1.00 q1: 0.00 q2: 0.00 q3: 0.00 ...&gt;</c>.</returns>
    public override string ToString() => ToStringWithTypeName(nameof(Quaternion));

    /// <summary>Formats this quaternion using an explicit Python class name.</summary>
    /// <param name="typeName">The Python class name to show in the representation.</param>
    /// <returns>The Python-style representation.</returns>
    internal string ToStringWithTypeName(string typeName) =>
        FormattableString.Invariant( // util.py:658-660
            $"<{typeName} q0: {Q0:F2} q1: {Q1:F2} q2: {Q2:F2} q3: {Q3:F2} (angle_z: {AngleZ})>");
}
