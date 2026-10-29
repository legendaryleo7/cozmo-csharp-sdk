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
/// Port of <c>ObservableObject</c> in <c>cozmo/objects.py</c>. On top of
/// <see cref="ObservableElement"/> it adds an engine object id, descriptive naming, pickup and
/// placement flags, and the state-update methods that the world layer (WP-11) calls when it
/// routes connected, located and observed object messages.
/// </para>
/// <para>
/// Message subscriptions intentionally live in the world layer, matching the Python design:
/// <c>cozmo/world.py</c> receives the CLAD messages, looks up the object by id, and forwards
/// the state to it. See <see cref="HandleObserved"/>,
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
    /// <para>
    /// Python's setter logs a warning when the id is reassigned rather than rejecting the
    /// change (objects.py:382-393), because the engine may remove and re-add a cube with a new
    /// id after the cube reconnects. The same warning is logged here.
    /// </para>
    /// <para>
    /// The value is <see langword="null"/> until the world allocates the object.
    /// </para>
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
    /// <remarks>Python's <c>pickupable</c> class attribute (objects.py:285).</remarks>
    public virtual bool Pickupable => false;

    /// <summary><see langword="true"/> when Cozmo can physically place objects on this type of object.</summary>
    /// <remarks>Python's <c>place_objects_on_this</c> class attribute (objects.py:287).</remarks>
    public virtual bool PlaceObjectsOnThis => false;

    /// <summary><see langword="true"/> when the object is currently connected to the robot.</summary>
    /// <remarks>
    /// Python creates this attribute dynamically in <c>_handle_connected_object_state</c>
    /// (objects.py:321-326) and gives it a permanent home on <c>LightCube</c>. The port keeps a
    /// single property on the base type so the connected-state handler can be shared.
    /// </remarks>
    public bool IsConnected { get; protected set; }

    /// <summary>Applies a robot-observed-object message.</summary>
    /// <param name="message">The CLAD observation message routed by the world.</param>
    /// <returns>A task that completes after the observed and, on first sight, appeared events are dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of <c>_recv_msg_robot_observed_object</c> (objects.py:401-407).</remarks>
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
    /// <remarks>
    /// Port of <c>_handle_connected_object_state</c> (objects.py:318-326). The message's
    /// identity fields are not needed by an object that the world already allocated; the
    /// Python implementation likewise ignores them and only resets the pose to the default.
    /// </remarks>
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
    /// Port of <c>_handle_located_object_state</c> (objects.py:328-356), including its
    /// old-timestamp guard: a state whose <c>lastObservedTimestamp</c> predates the last
    /// observation is ignored, but the guard is disabled while the stored timestamp is zero,
    /// mirroring Python's truthiness test.
    /// </para>
    /// <para>
    /// An <see cref="global::Anki.PoseState.Invalid"/> state is logged and invalidates the
    /// pose. A <see cref="global::Anki.PoseState.Dirty"/> state is inaccurate rather than
    /// invalid; Python mutates the pose's private accuracy flag, while this port builds a pose
    /// with the same position, rotation and origin but <c>isAccurate: false</c> because
    /// <see cref="Pose"/> exposes that flag as read-only.
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
