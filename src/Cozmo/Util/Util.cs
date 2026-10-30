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

/// <summary>Convenience factories and conversions for common SDK value types.</summary>
/// <remarks>
/// <para>
/// Most unit-specific factories are also available on their value types, such as
/// <see cref="Angle.FromDegrees"/>, <see cref="Distance.FromInches"/> and
/// <see cref="Pose.FromQuaternion"/>.
/// </para>
/// </remarks>
public static class Util
{
    /// <summary>Returns an <see cref="Angle"/> set to the specified number of degrees.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>A new <see cref="Angle"/>.</returns>
    public static Angle Degrees(double degrees) => Angle.FromDegrees(degrees);

    /// <summary>Returns an <see cref="Angle"/> set to the specified number of radians.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>A new <see cref="Angle"/>.</returns>
    public static Angle Radians(double radians) => Angle.FromRadians(radians);

    /// <summary>Returns a <see cref="Distance"/> set to the specified number of millimeters.</summary>
    /// <param name="millimeters">The distance in millimeters.</param>
    /// <returns>A new <see cref="Distance"/>.</returns>
    public static Distance DistanceMillimeters(double millimeters) => Distance.FromMillimeters(millimeters);

    /// <summary>Returns a <see cref="Distance"/> set to the specified number of inches.</summary>
    /// <param name="inches">The distance in inches.</param>
    /// <returns>A new <see cref="Distance"/>.</returns>
    public static Distance DistanceInches(double inches) => Distance.FromInches(inches);

    /// <summary>Returns a <see cref="Speed"/> set to the specified millimeters per second.</summary>
    /// <param name="millimetersPerSecond">The speed in millimeters per second.</param>
    /// <returns>A new <see cref="Speed"/>.</returns>
    public static Speed SpeedMillimetersPerSecond(double millimetersPerSecond) =>
        Speed.FromMillimetersPerSecond(millimetersPerSecond);

    /// <summary>Returns a <see cref="Pose"/> set to the pose given in quaternion format.</summary>
    /// <param name="x">X position in millimeters.</param>
    /// <param name="y">Y position in millimeters.</param>
    /// <param name="z">Z position in millimeters.</param>
    /// <param name="q0">The q0 (w) component.</param>
    /// <param name="q1">The q1 (i) component.</param>
    /// <param name="q2">The q2 (j) component.</param>
    /// <param name="q3">The q3 (k) component.</param>
    /// <param name="originId">The engine coordinate-frame id.</param>
    /// <param name="isAccurate">Whether the pose is believed to be accurate.</param>
    /// <returns>A new <see cref="Pose"/>.</returns>
    public static Pose PoseQuaternion(
        double x,
        double y,
        double z,
        double q0,
        double q1,
        double q2,
        double q3,
        int originId = 0,
        bool isAccurate = true) =>
        Pose.FromQuaternion(x, y, z, q0, q1, q2, q3, originId, isAccurate);

    /// <summary>Returns a <see cref="Pose"/> set to the pose given in z-angle format.</summary>
    /// <param name="x">X position in millimeters.</param>
    /// <param name="y">Y position in millimeters.</param>
    /// <param name="z">Z position in millimeters.</param>
    /// <param name="angleZ">The angle about the z axis.</param>
    /// <param name="originId">The engine coordinate-frame id.</param>
    /// <param name="isAccurate">Whether the pose is believed to be accurate.</param>
    /// <returns>A new <see cref="Pose"/>.</returns>
    public static Pose PoseZAngle(
        double x,
        double y,
        double z,
        Angle angleZ,
        int originId = 0,
        bool isAccurate = true) =>
        Pose.FromZAngle(x, y, z, angleZ, originId, isAccurate);

    /// <summary>Returns a <see cref="Rotation"/> set by a quaternion.</summary>
    /// <param name="q0">The q0 (w) component.</param>
    /// <param name="q1">The q1 (i) component.</param>
    /// <param name="q2">The q2 (j) component.</param>
    /// <param name="q3">The q3 (k) component.</param>
    /// <returns>A new <see cref="Rotation"/>.</returns>
    public static Rotation RotationQuaternion(double q0, double q1, double q2, double q3) =>
        Rotation.FromQuaternion(q0, q1, q2, q3);

    /// <summary>Returns a <see cref="Rotation"/> set by an angle about the z axis.</summary>
    /// <param name="angleZ">The angle about the z axis.</param>
    /// <returns>A new <see cref="Rotation"/>.</returns>
    public static Rotation RotationZAngle(Angle angleZ) => Rotation.FromZAngle(angleZ);

    /// <summary>Converts an angle about the z axis to quaternion components.</summary>
    /// <param name="angleZ">The z axis angle.</param>
    /// <returns>The q0, q1, q2 and q3 components of the corresponding quaternion.</returns>
    public static (double Q0, double Q1, double Q2, double Q3) AngleZToQuaternion(Angle angleZ)
    {
        double radians = angleZ.Radians;
        double q0 = Math.Cos(radians / 2.0); // util.py:829
        const double q1 = 0.0; // util.py:831
        const double q2 = 0.0; // util.py:833
        double q3 = Math.Sin(radians / 2.0); // util.py:835
        return (q0, q1, q2, q3);
    }
}
