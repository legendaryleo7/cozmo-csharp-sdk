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
using Anki.Cozmo.ExternalInterface;
using Microsoft.Extensions.Logging;

namespace Anki.Cozmo.Sdk;

/// <summary>The base type for objects in Cozmo's world.</summary>
/// <remarks>
/// <para>
/// Extends <see cref="ObservableElement"/> with an engine object id, descriptive naming,
/// pickup and placement flags, and state-update methods for connected, located and observed
/// object messages.
/// </para>
/// <para>
/// The world routes CLAD messages to objects by id. See <see cref="HandleObserved"/>,
/// <see cref="HandleConnectedObjectState"/> and <see cref="HandleLocatedObjectState"/>.
/// </para>
/// </remarks>
public class ObservableObject : ObservableElement
{
    private int? _objectId;

    /// <summary>Creates an object.</summary>
    /// <param name="connection">The connection used to send commands for this object.</param>
    /// <param name="world">The world that owns this object; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="objectId">The engine object id, or <see langword="null"/> before allocation.</param>
    /// <param name="timeProvider">Clock and timer source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this object.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public ObservableObject(
        CozmoConnection connection,
        IWorldContext? world = null,
        int? objectId = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(connection, world, timeProvider, dispatchParent, logger)
    {
        _objectId = objectId;
    }

    /// <summary>The internal id assigned to the object.</summary>
    /// <remarks>
    /// <para>The value is <see langword="null"/> until the world allocates the object.</para>
    /// <para>If the engine reassigns the id, the new value is applied and a warning is logged.</para>
    /// </remarks>
    public int? ObjectId
    {
        get => _objectId;
        set
        {
            if (_objectId is not null)
            {
                Logger.LogWarning(
                    "Changing object_id for {ObjectType} from {OldId} to {NewId}.",
                    GetType().Name,
                    _objectId.Value,
                    value);
            }
            else
            {
                Logger.LogDebug(
                    "Setting object_id for {ObjectType} to {NewId}.",
                    GetType().Name,
                    value);
            }

            _objectId = value;
        }
    }

    /// <summary>A descriptive name for this object instance.</summary>
    /// <remarks>Subclasses override this to include type-specific information.</remarks>
    public virtual string DescriptiveName =>
        FormattableString.Invariant(
            $"{GetType().Name} id={(_objectId is int id ? id.ToString(CultureInfo.InvariantCulture) : "None")}");

    /// <summary><see langword="true"/> when Cozmo can physically pick up this type of object.</summary>
    public virtual bool Pickupable => false;

    /// <summary><see langword="true"/> when Cozmo can physically place objects on this type of object.</summary>
    public virtual bool PlaceObjectsOnThis => false;

    /// <summary><see langword="true"/> when the object is currently connected to the robot.</summary>
    public bool IsConnected { get; protected set; }

    /// <summary>Applies a robot-observed-object message.</summary>
    /// <param name="message">The CLAD observation message routed by the world.</param>
    /// <returns>A task that completes after the observed and, on first sight, appeared events are dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public async Task HandleObserved(RobotObservedObject message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var changedFields = new HashSet<string>(StringComparer.Ordinal) { "pose" };
        Pose = Anki.Cozmo.Sdk.Pose.FromClad(message.pose);

        ImageBox imageBox = ImageBox.FromCladRect(message.img_rect);
        await OnObservedAsync(imageBox, message.timestamp, changedFields).ConfigureAwait(false);
    }

    /// <summary>Applies a connected-object-states message.</summary>
    /// <param name="message">The CLAD connected-object state routed by the world.</param>
    /// <returns>A task that completes after the connected event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>The identity fields are ignored; this handler resets the pose to its default.</remarks>
    public async Task HandleConnectedObjectState(ConnectedObjectState message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Pose = Anki.Cozmo.Sdk.Pose.CreateDefault();
        IsConnected = true;
        await DispatchEventAsync(new EvtObjectConnected(this, Connected: true)).ConfigureAwait(false);
    }

    /// <summary>Applies a located-object state message.</summary>
    /// <param name="message">The CLAD located-object state routed by the world.</param>
    /// <returns>A task that completes after the located event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// A state whose <c>lastObservedTimestamp</c> predates the last observation is ignored.
    /// This guard is disabled while the stored timestamp is zero.
    /// </para>
    /// <para>
    /// An <see cref="global::Anki.PoseState.Invalid"/> state is logged and invalidates the
    /// pose. A <see cref="global::Anki.PoseState.Dirty"/> state keeps the pose valid but marks
    /// it inaccurate.
    /// </para>
    /// </remarks>
    public async Task HandleLocatedObjectState(LocatedObjectState message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (LastObservedRobotTimestamp is uint lastObserved &&
            lastObserved > 0 &&
            lastObserved > message.lastObservedTimestamp)
        {
            Logger.LogWarning(
                "Ignoring old located object_state for {Object} (last_observed_robot_timestamp={LastObserved}).",
                this,
                lastObserved);
            return;
        }

        var changedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "last_observed_robot_timestamp",
            "pose",
        };

        LastObservedRobotTimestamp = message.lastObservedTimestamp;

        Pose locatedPose = Anki.Cozmo.Sdk.Pose.FromClad(message.pose);
        switch ((global::Anki.PoseState)message.poseState)
        {
            case global::Anki.PoseState.Invalid:
                Logger.LogError("Unexpected Invalid pose state received for {Object}.", this);
                locatedPose.Invalidate();
                break;

            case global::Anki.PoseState.Dirty:
                // Python sets the pose's private _is_accurate flag to False (objects.py:350-353).
                locatedPose = new Pose(
                    locatedPose.Position.X,
                    locatedPose.Position.Y,
                    locatedPose.Position.Z,
                    locatedPose.Rotation,
                    locatedPose.OriginId,
                    isAccurate: false);
                break;
        }

        Pose = locatedPose;
        await DispatchEventAsync(new EvtObjectLocated(this, changedFields, locatedPose)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override Task DispatchObservedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) =>
        DispatchEventAsync(new EvtObjectObserved(this, changedFields, imageBox, Pose));

    /// <inheritdoc />
    protected override Task DispatchAppearedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) =>
        DispatchEventAsync(new EvtObjectAppeared(this, changedFields, imageBox, Pose));

    /// <inheritdoc />
    protected override Task DispatchDisappearedEventAsync() =>
        DispatchEventAsync(new EvtObjectDisappeared(this));
}
