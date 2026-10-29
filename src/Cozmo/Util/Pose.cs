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

/// <summary>Represents where an object is in the world.</summary>
/// <remarks>
/// <para>Mirrors <c>Pose</c> in <c>cozmo/util.py</c>.</para>
/// <para>
/// Only poses with the same <see cref="OriginId"/> and a valid origin can be compared; see
/// <see cref="IsComparable"/>. The engine assigns a new origin whenever Cozmo is
/// de-localized, so poses from different origins must not be combined by callers even though
/// Python's arithmetic operators do not reject them.
/// </para>
/// <para>
/// Python mutates the origin id in <c>invalidate()</c> and through the <c>origin_id</c>
/// setter, so this type is a mutable class rather than a struct, matching the source. Pose
/// arithmetic mirrors Python exactly: the result is built with
/// <see cref="FromQuaternion"/> defaults, so its origin id is <c>0</c> and its accuracy flag
/// is <see langword="true"/> regardless of the operands.
/// </para>
/// <para>
/// The coordinate space is relative to Cozmo: the X axis points forward, Y points left and Z
/// points up.
/// </para>
/// </remarks>
public sealed class Pose
{
    private readonly Position _position;
    private readonly Quaternion _rotation;
    private readonly bool _isAccurate;
    private int _originId;

    /// <summary>Creates a pose from a position and a rotation.</summary>
    /// <param name="x">X position in millimeters.</param>
    /// <param name="y">Y position in millimeters.</param>
    /// <param name="z">Z position in millimeters.</param>
    /// <param name="rotation">The rotation component.</param>
    /// <param name="originId">The engine coordinate-frame id; defaults to <c>-1</c> (invalid), as in Python.</param>
    /// <param name="isAccurate">Whether the pose is believed to be accurate; defaults to <see langword="true"/>.</param>
    public Pose(double x, double y, double z, Quaternion rotation, int originId = -1, bool isAccurate = true)
    {
        _position = new Position(x, y, z);
        _rotation = rotation;
        _originId = originId;
        _isAccurate = isAccurate;
    }

    /// <summary>The position component of this pose.</summary>
    public Position Position => _position;

    /// <summary>The rotation component of this pose.</summary>
    public Quaternion Rotation => _rotation;

    /// <summary>An ID maintained by the engine that identifies the coordinate frame of this pose.</summary>
    /// <remarks>Python allows this to be assigned later when it was not originally defined.</remarks>
    public int OriginId
    {
        get => _originId;
        set => _originId = value;
    }

    /// <summary><see langword="true"/> when this is a valid, usable pose.</summary>
    /// <remarks>Mirrors Python's <c>is_valid</c>: the origin id must be non-negative.</remarks>
    public bool IsValid => OriginId >= 0;

    /// <summary><see langword="true"/> when this pose is both valid and believed to be accurate.</summary>
    /// <remarks>Mirrors Python's <c>is_accurate</c> (util.py:477-485).</remarks>
    public bool IsAccurate => IsValid && _isAccurate;

    /// <summary>Creates a pose from a position and a quaternion.</summary>
    /// <param name="x">X position in millimeters.</param>
    /// <param name="y">Y position in millimeters.</param>
    /// <param name="z">Z position in millimeters.</param>
    /// <param name="q0">The q0 (w) component.</param>
    /// <param name="q1">The q1 (i) component.</param>
    /// <param name="q2">The q2 (j) component.</param>
    /// <param name="q3">The q3 (k) component.</param>
    /// <param name="originId">The engine coordinate-frame id; defaults to <c>0</c>, as in Python's <c>pose_quaternion</c>.</param>
    /// <param name="isAccurate">Whether the pose is believed to be accurate.</param>
    /// <returns>A new <see cref="Pose"/>.</returns>
    /// <remarks>
    /// Mirrors Python's <c>pose_quaternion</c> (util.py:488). Python's helper has no
    /// <c>is_accurate</c> argument; it is exposed here because the underlying <see cref="Pose"/>
    /// constructor accepts it. Note that the Python <c>Pose</c> constructor itself defaults
    /// <c>origin_id</c> to <c>-1</c>, whereas this helper defaults it to <c>0</c>.
    /// </remarks>
    public static Pose FromQuaternion(
        double x,
        double y,
        double z,
        double q0,
        double q1,
        double q2,
        double q3,
        int originId = 0,
        bool isAccurate = true) =>
        new(x, y, z, new Quaternion(q0, q1, q2, q3), originId, isAccurate);

    /// <summary>Creates a pose from a position and a rotation about the z axis.</summary>
    /// <param name="x">X position in millimeters.</param>
    /// <param name="y">Y position in millimeters.</param>
    /// <param name="z">Z position in millimeters.</param>
    /// <param name="angleZ">The angle about the z axis.</param>
    /// <param name="originId">The engine coordinate-frame id; defaults to <c>0</c>, as in Python's <c>pose_z_angle</c>.</param>
    /// <param name="isAccurate">Whether the pose is believed to be accurate.</param>
    /// <returns>A new <see cref="Pose"/>.</returns>
    /// <remarks>
    /// Mirrors Python's <c>pose_z_angle</c> (util.py:492). Python's helper has no
    /// <c>is_accurate</c> argument; it is exposed here because the underlying <see cref="Pose"/>
    /// constructor accepts it. Note that the Python <c>Pose</c> constructor itself defaults
    /// <c>origin_id</c> to <c>-1</c>, whereas this helper defaults it to <c>0</c>.
    /// </remarks>
    public static Pose FromZAngle(
        double x,
        double y,
        double z,
        Angle angleZ,
        int originId = 0,
        bool isAccurate = true) =>
        new(x, y, z, Quaternion.FromZAngle(angleZ), originId, isAccurate);

    /// <summary>Creates the default pose: the identity rotation at the origin, with origin id <c>-1</c>.</summary>
    /// <returns>A new <see cref="Pose"/>.</returns>
    /// <remarks>Mirrors Python's private <c>Pose._create_default</c> (util.py:358).</remarks>
    public static Pose CreateDefault() =>
        new(0.0, 0.0, 0.0, new Quaternion(1.0, 0.0, 0.0, 0.0), originId: -1);

    /// <summary>Creates a pose from a generated CLAD pose structure.</summary>
    /// <param name="cladPose">The CLAD pose; its components are single-precision floats.</param>
    /// <returns>A new <see cref="Pose"/>.</returns>
    /// <remarks>Mirrors Python's private <c>Pose._create_from_clad</c> (util.py:352).</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cladPose"/> is <see langword="null"/>.</exception>
    public static Pose FromClad(PoseStruct3d cladPose)
    {
        ArgumentNullException.ThrowIfNull(cladPose);
        return new Pose(
            cladPose.x,
            cladPose.y,
            cladPose.z,
            new Quaternion(cladPose.q0, cladPose.q1, cladPose.q2, cladPose.q3),
            (int)cladPose.originID);
    }

    /// <summary>Marks this pose as invalid and unusable by setting the origin id to <c>-1</c>.</summary>
    public void Invalidate() => _originId = -1; // util.py:424-426

    /// <summary>Determines whether this pose and another pose can be compared.</summary>
    /// <param name="otherPose">The other pose to compare against.</param>
    /// <returns><see langword="true"/> when both poses are valid and share the same origin id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="otherPose"/> is <see langword="null"/>.</exception>
    public bool IsComparable(Pose otherPose)
    {
        ArgumentNullException.ThrowIfNull(otherPose);
        return IsValid && otherPose.IsValid && OriginId == otherPose.OriginId; // util.py:438-439
    }

    /// <summary>Creates a new pose such that <paramref name="newPose"/>'s origin is this pose's location.</summary>
    /// <param name="newPose">The pose whose origin is being changed.</param>
    /// <returns>A pose expressed relative to this pose's origin.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newPose"/> is <see langword="null"/>.</exception>
    public Pose DefinePoseRelativeThis(Pose newPose)
    {
        ArgumentNullException.ThrowIfNull(newPose);

        double x = Position.X;
        double y = Position.Y;
        double z = Position.Z;
        Angle angleZ = Rotation.AngleZ;
        double newX = newPose.Position.X;
        double newY = newPose.Position.Y;
        double newZ = newPose.Position.Z;
        Angle newAngleZ = newPose.Rotation.AngleZ;

        double cosAngle = Math.Cos(angleZ.Radians);
        double sinAngle = Math.Sin(angleZ.Radians);
        double resX = x + (cosAngle * newX) - (sinAngle * newY);
        double resY = y + (sinAngle * newX) + (cosAngle * newY);
        double resZ = z + newZ;
        Angle resAngle = angleZ + newAngleZ;
        return new Pose(resX, resY, resZ, Quaternion.FromZAngle(resAngle), OriginId); // util.py:406-417
    }

    /// <summary>Converts this pose to a 4x4 matrix.</summary>
    /// <returns>A matrix representing this pose's position and rotation.</returns>
    public Matrix44 ToMatrix() => Rotation.ToMatrix(Position.X, Position.Y, Position.Z);

    /// <summary>Encodes this pose as a generated CLAD pose structure.</summary>
    /// <returns>The CLAD <see cref="PoseStruct3d"/> representation.</returns>
    /// <remarks>
    /// Python's CLAD binding rejects negative origin ids with a <c>ValueError</c>; this port
    /// raises the corresponding <see cref="InvalidOperationException"/> before the cast that
    /// would otherwise wrap a negative value into an unsigned origin id.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The origin id is negative.</exception>
    public PoseStruct3d EncodePose()
    {
        if (OriginId < 0)
        {
            throw new InvalidOperationException(
                $"PoseStruct3d.originID must be between 0 and {uint.MaxValue}. Got {OriginId}.");
        }

        return new PoseStruct3d(
            (float)Position.X,
            (float)Position.Y,
            (float)Position.Z,
            (float)Rotation.Q0,
            (float)Rotation.Q1,
            (float)Rotation.Q2,
            (float)Rotation.Q3,
            (uint)OriginId); // util.py:419-422
    }

    /// <summary>Adds two poses.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A pose with the summed position and z rotation; origin id <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException">An operand is <see langword="null"/>.</exception>
    public static Pose operator +(Pose left, Pose right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        Vector3 position = left.Position + right.Position;
        Quaternion rotation = left.Rotation + right.Rotation;
        return FromQuaternion(position.X, position.Y, position.Z, rotation.Q0, rotation.Q1, rotation.Q2, rotation.Q3);
    }

    /// <summary>Subtracts one pose from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A pose with the subtracted position and z rotation; origin id <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException">An operand is <see langword="null"/>.</exception>
    public static Pose operator -(Pose left, Pose right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        Vector3 position = left.Position - right.Position;
        Quaternion rotation = left.Rotation - right.Rotation;
        return FromQuaternion(position.X, position.Y, position.Z, rotation.Q0, rotation.Q1, rotation.Q2, rotation.Q3);
    }

    /// <summary>Multiplies a pose's position and z rotation by a scalar.</summary>
    /// <param name="pose">The pose.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>A scaled pose with origin id <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pose"/> is <see langword="null"/>.</exception>
    public static Pose operator *(Pose pose, double scalar)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Vector3 position = pose.Position * scalar;
        Quaternion rotation = pose.Rotation * scalar;
        return FromQuaternion(position.X, position.Y, position.Z, rotation.Q0, rotation.Q1, rotation.Q2, rotation.Q3);
    }

    /// <summary>Divides a pose's position and z rotation by a scalar.</summary>
    /// <param name="pose">The pose.</param>
    /// <param name="scalar">The scalar divisor.</param>
    /// <returns>A scaled pose with origin id <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pose"/> is <see langword="null"/>.</exception>
    /// <exception cref="DivideByZeroException">The divisor is zero, as Python raises <c>ZeroDivisionError</c>.</exception>
    public static Pose operator /(Pose pose, double scalar)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Vector3 position = pose.Position / scalar;
        Quaternion rotation = pose.Rotation / scalar;
        return FromQuaternion(position.X, position.Y, position.Z, rotation.Q0, rotation.Q1, rotation.Q2, rotation.Q3);
    }

    /// <summary>Returns Python's <c>repr</c> representation of this pose.</summary>
    /// <returns>A string such as <c>&lt;Pose &lt;Position ...&gt; &lt;Quaternion ...&gt; origin_id=5&gt;</c>.</returns>
    public override string ToString() => // util.py:364-365
        FormattableString.Invariant($"<{nameof(Pose)} {Position} {Rotation} origin_id={OriginId}>");
}
