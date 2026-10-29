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
using Microsoft.Extensions.Logging;

namespace Anki.Cozmo.Sdk;

/// <summary>
/// An object defined by the SDK and bound to a specific custom object type, such as
/// <see cref="CustomObjectTypes.CustomType00"/>.
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>CustomObject</c> in <c>cozmo/objects.py</c>. The engine creates one of these
/// for every unique object id it observes with the matching custom markers; instances are not
/// added to the world until the robot sees them.
/// </para>
/// <para>
/// Python stores the dimensions and marker sizes as opaque floats supplied when the custom
/// object was defined. The port exposes them unchanged so the world layer (WP-11) can create
/// archetypes with exactly the values that were defined in the engine.
/// </para>
/// </remarks>
public sealed class CustomObject : ObservableObject
{
    /// <summary>Creates a custom object instance.</summary>
    /// <param name="connection">The connection used to send commands for this object.</param>
    /// <param name="world">The world that owns this object; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="objectType">The custom object type this instance is bound to.</param>
    /// <param name="xSizeMm">Size of the object along its X axis, in millimeters.</param>
    /// <param name="ySizeMm">Size of the object along its Y axis, in millimeters.</param>
    /// <param name="zSizeMm">Size of the object along its Z axis, in millimeters.</param>
    /// <param name="markerWidthMm">Width in millimeters of the marker on the object.</param>
    /// <param name="markerHeightMm">Height in millimeters of the marker on the object.</param>
    /// <param name="isUnique">True when there should only be one of this object type in the world.</param>
    /// <param name="objectId">The engine object id, or <see langword="null"/> before allocation.</param>
    /// <param name="timeProvider">Clock and timer source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this object.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/> or <paramref name="objectType"/> is <see langword="null"/>.
    /// </exception>
    public CustomObject(
        CozmoConnection connection,
        IWorldContext? world,
        CladEnum<ObjectType> objectType,
        double xSizeMm,
        double ySizeMm,
        double zSizeMm,
        double markerWidthMm,
        double markerHeightMm,
        bool isUnique,
        int? objectId = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(connection, world, objectId, timeProvider, dispatchParent, logger)
    {
        ArgumentNullException.ThrowIfNull(objectType);
        ObjectType = objectType;
        XSizeMm = xSizeMm;
        YSizeMm = ySizeMm;
        ZSizeMm = zSizeMm;
        MarkerWidthMm = markerWidthMm;
        MarkerHeightMm = markerHeightMm;
        IsUnique = isUnique;
    }

    /// <summary>The custom object type this object is bound to.</summary>
    public CladEnum<ObjectType> ObjectType { get; }

    /// <summary>Size of this object along its X axis, in millimeters.</summary>
    public double XSizeMm { get; }

    /// <summary>Size of this object along its Y axis, in millimeters.</summary>
    public double YSizeMm { get; }

    /// <summary>Size of this object along its Z axis, in millimeters.</summary>
    public double ZSizeMm { get; }

    /// <summary>Width in millimeters of the marker on this object.</summary>
    public double MarkerWidthMm { get; }

    /// <summary>Height in millimeters of the marker on this object.</summary>
    public double MarkerHeightMm { get; }

    /// <summary><see langword="true"/> when there should only be one of this object type in the world.</summary>
    public bool IsUnique { get; }

    /// <inheritdoc />
    public override string DescriptiveName =>
        FormattableString.Invariant(
            $"{ObjectType.Name} id={(ObjectId is int id ? id.ToString(CultureInfo.InvariantCulture) : "None")}");
}
