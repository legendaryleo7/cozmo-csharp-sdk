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

/// <summary>A light cube: four LEDs that Cozmo can actively manipulate and communicate with.</summary>
/// <remarks>
/// <para>
/// Port of <c>LightCube</c> in <c>cozmo/objects.py</c>. A cube has a fixed
/// <see cref="CubeId"/> identifying which physical cube it is, plus an engine-assigned
/// <see cref="ObservableObject.ObjectId"/> that can change when the cube reconnects.
/// </para>
/// <para>
/// The light commands are built here and sent through the connection, because they are
/// cube-specific rather than world-specific. Inbound messages are still routed by the world
/// layer (WP-11), which calls the state handlers on this instance.
/// </para>
/// </remarks>
public sealed class LightCube : ObservableObject
{
    /// <summary>Voltage at which a cube's battery is considered empty.</summary>
    /// <remarks>Python <c>LightCube.EMPTY_VOLTAGE</c> (objects.py:435).</remarks>
    public const double EmptyVoltage = 1.0;

    /// <summary>Voltage at which a cube's battery is considered full.</summary>
    /// <remarks>Python <c>LightCube.FULL_VOLTAGE</c> (objects.py:437).</remarks>
    public const double FullVoltage = 1.5;

    private readonly CladEnum<ObjectType> _cubeId;

    /// <summary>Creates a light cube.</summary>
    /// <param name="cubeId">Which of the three cube ids this instance represents.</param>
    /// <param name="connection">The connection used to send light commands.</param>
    /// <param name="world">The world that owns this cube; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="objectId">The engine object id, or <see langword="null"/> before allocation.</param>
    /// <param name="timeProvider">Clock and timer source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this cube.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/> or <paramref name="cubeId"/> is <see langword="null"/>.
    /// </exception>
    public LightCube(
        CladEnum<ObjectType> cubeId,
        CozmoConnection connection,
        IWorldContext? world = null,
        int? objectId = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(connection, world, objectId, timeProvider, dispatchParent, logger)
    {
        ArgumentNullException.ThrowIfNull(cubeId);
        _cubeId = cubeId;
    }

    /// <inheritdoc />
    public override bool Pickupable => true;

    /// <inheritdoc />
    public override bool PlaceObjectsOnThis => true;

    /// <summary>The Light Cube id.</summary>
    /// <remarks>
    /// This is one of <see cref="CustomObjectTypes.LightCube1Id"/>,
    /// <see cref="CustomObjectTypes.LightCube2Id"/> and
    /// <see cref="CustomObjectTypes.LightCube3Id"/>. It is not the same as
    /// <see cref="ObservableObject.ObjectId"/>.
    /// </remarks>
    public CladEnum<ObjectType> CubeId => _cubeId;

    /// <summary>Battery voltage, or <see langword="null"/> when no voltage reading has been received.</summary>
    public double? BatteryVoltage { get; private set; }

    /// <summary><see langword="true"/> while the cube's accelerometer indicates that it is moving.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>The time the cube was last tapped, or <see langword="null"/> if it was never tapped.</summary>
    public DateTimeOffset? LastTappedTime { get; private set; }

    /// <summary>The robot's timestamp of the last tapped event, in milliseconds relative to the robot epoch.</summary>
    public uint? LastTappedRobotTimestamp { get; private set; }

    /// <summary>The time the cube was last moved, or <see langword="null"/> if it was never moved.</summary>
    public DateTimeOffset? LastMovedTime { get; private set; }

    /// <summary>The time the cube started moving when it was last moved.</summary>
    public DateTimeOffset? LastMovedStartTime { get; private set; }

    /// <summary>The robot's timestamp of the last move event, in milliseconds relative to the robot epoch.</summary>
    public uint? LastMovedRobotTimestamp { get; private set; }

    /// <summary>The robot's timestamp of when the cube started moving when it was last moved.</summary>
    public uint? LastMovedStartRobotTimestamp { get; private set; }

    /// <summary>Battery level as a percentage, or <see langword="null"/> when no voltage is known.</summary>
    /// <remarks>Port of Python <c>battery_percentage</c> (objects.py:518-529).</remarks>
    public double? BatteryPercentage
    {
        get
        {
            if (BatteryVoltage is not double voltage)
            {
                // No voltage measurement has been received yet.
                return null;
            }

            if (voltage >= FullVoltage)
            {
                return 100.0;
            }

            if (voltage <= EmptyVoltage)
            {
                return 0.0;
            }

            return 100.0 * ((voltage - EmptyVoltage) / (FullVoltage - EmptyVoltage));
        }
    }

    /// <summary>String representation of the battery level.</summary>
    /// <remarks>
    /// Returns <c>"Unknown"</c> until a voltage reading is received, otherwise the rounded
    /// percentage followed by <c>%</c>, mirroring Python <c>battery_str</c> (objects.py:531-537).
    /// </remarks>
    public string BatteryStr
    {
        get
        {
            if (BatteryPercentage is not double percentage)
            {
                return "Unknown";
            }

            return percentage.ToString("F0", CultureInfo.InvariantCulture) + "%";
        }
    }

    /// <inheritdoc />
    public override string DescriptiveName =>
        FormattableString.Invariant(
            $"{GetType().Name} {CubeId.Name} id={(ObjectId is int id ? id.ToString(CultureInfo.InvariantCulture) : "None")}");

    /// <summary>Waits for the cube to receive a tap event.</summary>
    /// <param name="timeout">
    /// Maximum time to wait, or <see langword="null"/> to wait indefinitely, matching Python's
    /// <c>timeout=None</c> default.
    /// </param>
    /// <returns>The tap event that was received.</returns>
    /// <exception cref="TimeoutException">No tap arrived before <paramref name="timeout"/>.</exception>
    /// <exception cref="SdkShutdownException">The cube had already stopped dispatching.</exception>
    public Task<EvtObjectTapped> WaitForTapAsync(TimeSpan? timeout = null) =>
        WaitFor<EvtObjectTapped>(timeout: timeout ?? Timeout.InfiniteTimeSpan);

    /// <summary>Sets all four LEDs on the cube to the same light.</summary>
    /// <param name="light">The light settings to apply to every corner.</param>
    /// <returns>A task that completes after the LED message is sent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="light"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The cube has no valid object id yet.</exception>
    /// <remarks>Port of Python <c>LightCube.set_lights</c> (objects.py:617-628).</remarks>
    public Task SetLightsAsync(Light light)
    {
        ArgumentNullException.ThrowIfNull(light);

        SetAllActiveObjectLEDs message = CreateLedMessage();
        for (int index = 0; index < 4; index++)
        {
            Lights.SetLight(message, index, light);
        }

        return SendLedMessageAsync(message);
    }

    /// <summary>Sets the cube's LEDs from a list.</summary>
    /// <param name="lights">
    /// Either one light, applied to all four corners, or exactly four lights applied in corner
    /// order. This overload is a C# convenience; Python exposes the one-light
    /// <c>set_lights</c> and the four-argument <c>set_light_corners</c>.
    /// </param>
    /// <returns>A task that completes after the LED message is sent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lights"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="lights"/> does not contain one or four lights.</exception>
    /// <exception cref="InvalidOperationException">The cube has no valid object id yet.</exception>
    public Task SetLightsAsync(IReadOnlyList<Light> lights)
    {
        ArgumentNullException.ThrowIfNull(lights);
        return lights.Count switch
        {
            1 => SetLightsAsync(lights[0]),
            4 => SetLightCornersAsync(lights[0], lights[1], lights[2], lights[3]),
            _ => throw new ArgumentException(
                "Expected either one light or four corner lights.",
                nameof(lights)),
        };
    }

    /// <summary>Sets the light for each corner of the cube.</summary>
    /// <param name="light1">Light for corner 0, or <see langword="null"/> to leave that slot unset.</param>
    /// <param name="light2">Light for corner 1, or <see langword="null"/> to leave that slot unset.</param>
    /// <param name="light3">Light for corner 2, or <see langword="null"/> to leave that slot unset.</param>
    /// <param name="light4">Light for corner 3, or <see langword="null"/> to leave that slot unset.</param>
    /// <returns>A task that completes after the LED message is sent.</returns>
    /// <exception cref="InvalidOperationException">The cube has no valid object id yet.</exception>
    /// <remarks>
    /// Port of Python <c>LightCube.set_light_corners</c> (objects.py:602-612). The corner
    /// numbering is the CLAD array order; Python deliberately leaves the physical corner
    /// mapping undocumented.
    /// </remarks>
    public Task SetLightCornersAsync(Light? light1, Light? light2, Light? light3, Light? light4)
    {
        SetAllActiveObjectLEDs message = CreateLedMessage();
        Light?[] corners = { light1, light2, light3, light4 };
        for (int index = 0; index < corners.Length; index++)
        {
            if (corners[index] is Light light)
            {
                Lights.SetLight(message, index, light);
            }
        }

        return SendLedMessageAsync(message);
    }

    /// <summary>Turns off all the LEDs on the cube.</summary>
    /// <returns>A task that completes after the LED message is sent.</returns>
    /// <exception cref="InvalidOperationException">The cube has no valid object id yet.</exception>
    /// <remarks>Port of Python <c>LightCube.set_lights_off</c> (objects.py:630-632).</remarks>
    public Task SetLightsOffAsync() => SetLightsAsync(Lights.Off);

    /// <summary>Applies an object-tapped message.</summary>
    /// <param name="message">The CLAD tap message routed by the world.</param>
    /// <returns>A task that completes after the tapped event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of <c>_recv_msg_object_tapped</c> (objects.py:535-541).</remarks>
    public async Task HandleObjectTapped(ObjectTapped message)
    {
        ArgumentNullException.ThrowIfNull(message);

        DateTimeOffset now = TimeProvider.GetUtcNow();
        LastEventTime = now;
        LastTappedTime = now;
        LastTappedRobotTimestamp = message.timestamp;

        int tapIntensity = message.tapPos - message.tapNeg;
        await DispatchEventAsync(new EvtObjectTapped(
            this,
            message.numTaps,
            message.tapTime,
            tapIntensity)).ConfigureAwait(false);
    }

    /// <summary>Applies an object-moved message.</summary>
    /// <param name="message">The CLAD move message routed by the world.</param>
    /// <returns>A task that completes after the moving-started or moving event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of <c>_recv_msg_object_moved</c> (objects.py:543-568).</remarks>
    public async Task HandleObjectMoved(ObjectMoved message)
    {
        ArgumentNullException.ThrowIfNull(message);

        DateTimeOffset now = TimeProvider.GetUtcNow();
        bool startedMoving = !IsMoving;
        IsMoving = true;
        LastEventTime = now;
        LastMovedTime = now;
        LastMovedRobotTimestamp = message.timestamp;

        Pose?.Invalidate();

        var acceleration = new Vector3(message.accel.x, message.accel.y, message.accel.z);
        if (startedMoving)
        {
            LastMovedStartTime = now;
            LastMovedStartRobotTimestamp = message.timestamp;
            await DispatchEventAsync(new EvtObjectMovingStarted(this, acceleration)).ConfigureAwait(false);
            return;
        }

        double moveDuration = LastMovedStartTime is DateTimeOffset startTime
            ? (now - startTime).TotalSeconds
            : 0.0;
        await DispatchEventAsync(new EvtObjectMoving(this, acceleration, moveDuration)).ConfigureAwait(false);
    }

    /// <summary>Applies an object-stopped-moving message.</summary>
    /// <param name="message">The CLAD stop message routed by the world.</param>
    /// <returns>A task that completes after the moving-stopped event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Port of <c>_recv_msg_object_stopped_moving</c> (objects.py:570-582). A stop that
    /// arrives while the cube is not marked as moving reports a zero duration, matching the
    /// Python comment about very short movements.
    /// </remarks>
    public async Task HandleObjectStoppedMoving(ObjectStoppedMoving message)
    {
        ArgumentNullException.ThrowIfNull(message);

        double moveDuration = 0.0;
        if (IsMoving)
        {
            IsMoving = false;
            if (LastMovedStartTime is DateTimeOffset startTime)
            {
                moveDuration = (TimeProvider.GetUtcNow() - startTime).TotalSeconds;
            }
        }

        await DispatchEventAsync(new EvtObjectMovingStopped(this, moveDuration)).ConfigureAwait(false);
    }

    /// <summary>Applies an object-power-level message.</summary>
    /// <param name="message">The CLAD power message routed by the world.</param>
    /// <returns>A completed task; no event is raised.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of <c>_recv_msg_object_power_level</c> (objects.py:584-585).</remarks>
    public Task HandleObjectPowerLevel(ObjectPowerLevel message)
    {
        ArgumentNullException.ThrowIfNull(message);
        BatteryVoltage = message.batteryLevel * 0.01;
        return Task.CompletedTask;
    }

    /// <summary>Applies an object-connection-state message.</summary>
    /// <param name="message">The CLAD connection message routed by the world.</param>
    /// <returns>A task that completes after any connect-changed event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of <c>_recv_msg_object_connection_state</c> (objects.py:587-595).</remarks>
    public async Task HandleObjectConnectionState(ObjectConnectionState message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (IsConnected == message.connected)
        {
            return;
        }

        if (message.connected)
        {
            Logger.LogInformation("Object connected: {Object}.", this);
        }
        else
        {
            Logger.LogInformation("Object disconnected: {Object}.", this);
        }

        IsConnected = message.connected;
        await DispatchEventAsync(new EvtObjectConnectChanged(this, IsConnected)).ConfigureAwait(false);
    }

    private SetAllActiveObjectLEDs CreateLedMessage()
    {
        int objectId = ObjectId ?? throw new InvalidOperationException(
            "Cannot set cube lights before the engine has assigned an object id.");
        if (objectId < 0)
        {
            throw new InvalidOperationException(
                $"Cannot set cube lights for negative object id {objectId}.");
        }

        return new SetAllActiveObjectLEDs { objectID = (uint)objectId };
    }

    private Task SendLedMessageAsync(SetAllActiveObjectLEDs message) =>
        Connection.SendAsync(new MessageGameToEngine { SetAllActiveObjectLEDs = message });
}
