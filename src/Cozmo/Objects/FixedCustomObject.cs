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

using System.Globalization;

namespace Anki.Cozmo.Sdk;

/// <summary>A fixed object defined by the SDK, placed in the world at a known pose.</summary>
/// <remarks>
/// <para>
/// Port of <c>FixedCustomObject</c> in <c>cozmo/objects.py</c> (objects.py:872-939). These
/// objects cannot be observed by the robot, so their pose never changes; they let callers make
/// Cozmo aware of obstacles that have no markers.
/// </para>
/// <para>
/// Unlike <see cref="ObservableObject"/>, this type is deliberately not a
/// <see cref="Dispatcher"/>; Python does not derive it from <c>ObservableElement</c> either.
/// </para>
/// <para>
/// Python has no explicit definition flag: a fixed object is defined once the engine returns
/// its object id, so <see cref="IsDefined"/> exposes that state without adding a field the
/// source does not have.
/// </para>
/// </remarks>
public sealed class FixedCustomObject
{
    private int? _objectId;

    /// <summary>Creates a fixed custom object.</summary>
    /// <param name="pose">The pose of the object in the world.</param>
    /// <param name="xSizeMm">Length of the object along its X axis, in millimeters.</param>
    /// <param name="ySizeMm">Length of the object along its Y axis, in millimeters.</param>
    /// <param name="zSizeMm">Length of the object along its Z axis, in millimeters.</param>
    /// <param name="objectId">The engine object id, or <see langword="null"/> until the engine assigns one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pose"/> is <see langword="null"/>.</exception>
    public FixedCustomObject(
        Pose pose,
        double xSizeMm,
        double ySizeMm,
        double zSizeMm,
        int? objectId = null)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Pose = pose;
        XSizeMm = xSizeMm;
        YSizeMm = ySizeMm;
        ZSizeMm = zSizeMm;
        _objectId = objectId;
    }

    /// <summary>The internal id assigned to the object.</summary>
    /// <remarks>
    /// Python raises <c>ValueError</c> when the id is changed after being set
    /// (objects.py:912-918), unlike <see cref="ObservableObject.ObjectId"/>, which merely logs
    /// a warning. This port raises <see cref="InvalidOperationException"/> for the same
    /// condition because C# has no equivalent of Python's built-in <c>ValueError</c> used for
    /// argument values.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The id has already been set.</exception>
    public int? ObjectId
    {
        get => _objectId;
        set
        {
            if (_objectId is not null)
            {
                throw new InvalidOperationException(
                    $"Cannot change object ID once set (from {_objectId.Value} to {value}).");
            }

            _objectId = value;
        }
    }

    /// <summary><see langword="true"/> once the engine has assigned an object id.</summary>
    public bool IsDefined => _objectId is not null;

    /// <summary>The pose of the object in the world.</summary>
    public Pose Pose { get; }

    /// <summary>Length of the object along its X axis, in millimeters.</summary>
    public double XSizeMm { get; }

    /// <summary>Length of the object along its Y axis, in millimeters.</summary>
    public double YSizeMm { get; }

    /// <summary>Length of the object along its Z axis, in millimeters.</summary>
    public double ZSizeMm { get; }

    /// <summary>Always <see langword="false"/>: fixed objects cannot be observed.</summary>
    /// <remarks>Python's <c>FixedCustomObject.is_visible</c> class attribute (objects.py:881).</remarks>
    public bool IsVisible => false;

    /// <summary>Returns a diagnostic representation of this fixed object.</summary>
    /// <returns>
    /// A string in the form <c>&lt;FixedCustomObject pose=... object_id=... x_size_mm=...&gt;</c>.
    /// </returns>
    public override string ToString() =>
        FormattableString.Invariant(
            $"<{nameof(FixedCustomObject)} pose={Pose} object_id={(_objectId is int id ? id.ToString(CultureInfo.InvariantCulture) : "None")} x_size_mm={XSizeMm:F1} y_size_mm={YSizeMm:F1} z_size_mm={ZSizeMm:F1}>");
}
