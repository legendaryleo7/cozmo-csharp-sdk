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

using System.Reflection;
using Anki;
using Anki.Cozmo;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;

namespace Cozmo.Tests;

/// <summary>Tests the WP-08 port of <c>cozmo.objects</c>.</summary>
public sealed class ObjectTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ObservationRaisesObservedThenAppearedAndUpdatesFields()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var obj = new ObservableObject(connection, timeProvider: clock);

        var observed = new List<EvtObjectObserved>();
        var appeared = new List<EvtObjectAppeared>();
        obj.AddHandler<EvtObjectObserved>(observed.Add);
        obj.AddHandler<EvtObjectAppeared>(appeared.Add);

        Assert.Equal(TimeSpan.MaxValue, obj.TimeSinceLastSeen);
        Assert.False(obj.IsVisible);

        var first = new RobotObservedObject
        {
            timestamp = 100,
            objectID = 7,
            pose = new PoseStruct3d(10.0f, 20.0f, 30.0f, 1.0f, 0.0f, 0.0f, 0.0f, 5),
            img_rect = new CladRect(1.0f, 2.0f, 3.0f, 4.0f),
        };
        await obj.HandleObserved(first);

        Assert.True(obj.IsVisible);
        Assert.Single(observed);
        Assert.Single(appeared);
        Assert.Same(obj, observed[0].Obj);
        Assert.Same(obj, appeared[0].Obj);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), observed[0].ImageBox);
        Assert.Equal(10.0, observed[0].Pose!.Position.X);
        Assert.Equal(clock.GetUtcNow(), obj.LastObservedTime);
        Assert.Equal(clock.GetUtcNow(), obj.LastEventTime);
        Assert.Equal(100u, obj.LastObservedRobotTimestamp);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), obj.LastObservedImageBox);

        var expectedUpdated = new HashSet<string>(StringComparer.Ordinal)
        {
            "pose",
            "last_observed_time",
            "last_observed_robot_timestamp",
            "last_event_time",
            "last_observed_image_box",
        };
        Assert.True(observed[0].Updated.SetEquals(expectedUpdated));
        Assert.True(appeared[0].Updated.SetEquals(expectedUpdated));

        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(100), obj.TimeSinceLastSeen);

        var second = new RobotObservedObject
        {
            timestamp = 200,
            objectID = 7,
            pose = new PoseStruct3d(11.0f, 20.0f, 30.0f, 1.0f, 0.0f, 0.0f, 0.0f, 5),
            img_rect = new CladRect(5.0f, 6.0f, 7.0f, 8.0f),
        };
        await obj.HandleObserved(second);

        Assert.Equal(2, observed.Count);
        Assert.Single(appeared);
        Assert.Equal(11.0, observed[1].Pose!.Position.X);
        Assert.Equal(200u, obj.LastObservedRobotTimestamp);
        Assert.Equal(TimeSpan.Zero, obj.TimeSinceLastSeen);
    }

    [Fact]
    public async Task VisibilityExpiresAfterTimeout()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var obj = new ObservableObject(connection, timeProvider: clock);

        var disappeared = new List<EvtObjectDisappeared>();
        var appeared = new List<EvtObjectAppeared>();
        obj.AddHandler<EvtObjectDisappeared>(disappeared.Add);
        obj.AddHandler<EvtObjectAppeared>(appeared.Add);

        Assert.Equal(ObservableElement.ObjectVisibilityTimeout, obj.VisibilityTimeout);
        Assert.Equal(TimeSpan.FromSeconds(0.4), obj.VisibilityTimeout);

        await obj.HandleObserved(ObservedMessage(timestamp: 10, x: 1.0f));

        clock.Advance(TimeSpan.FromMilliseconds(399));
        Assert.True(obj.IsVisible);
        Assert.Empty(disappeared);

        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.False(obj.IsVisible);
        Assert.Single(disappeared);
        Assert.Same(obj, disappeared[0].Obj);

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Single(disappeared);

        await obj.HandleObserved(ObservedMessage(timestamp: 20, x: 2.0f));
        Assert.True(obj.IsVisible);
        Assert.Equal(2, appeared.Count);
    }

    [Fact]
    public async Task ConnectedObjectStateResetsPoseAndReportsConnection()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var obj = new ObservableObject(connection, timeProvider: clock);

        var connected = new List<EvtObjectConnected>();
        obj.AddHandler<EvtObjectConnected>(connected.Add);

        await obj.HandleObserved(ObservedMessage(timestamp: 10, x: 1.0f));
        Assert.True(obj.Pose!.IsValid);

        await obj.HandleConnectedObjectState(new ConnectedObjectState(
            objectID: 7,
            objectFamily: (int)ObjectFamily.LightCube,
            objectType: (int)ObjectType.Block_LIGHTCUBE1));

        Assert.Single(connected);
        Assert.Same(obj, connected[0].Obj);
        Assert.True(connected[0].Connected);
        Assert.True(obj.IsConnected);
        Assert.False(obj.Pose!.IsValid);
        Assert.Equal(0.0, obj.Pose.Position.X);
    }

    [Fact]
    public async Task OldLocatedStateIsRejected()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var obj = new ObservableObject(connection, timeProvider: clock);

        var located = new List<EvtObjectLocated>();
        obj.AddHandler<EvtObjectLocated>(located.Add);

        await obj.HandleObserved(ObservedMessage(timestamp: 100, x: 10.0f));

        await obj.HandleLocatedObjectState(LocatedMessage(lastObservedTimestamp: 99, x: 500.0f));
        Assert.Empty(located);
        Assert.Equal(100u, obj.LastObservedRobotTimestamp);
        Assert.Equal(10.0, obj.Pose!.Position.X);

        await obj.HandleLocatedObjectState(LocatedMessage(lastObservedTimestamp: 101, x: 500.0f));
        Assert.Single(located);
        Assert.Equal(101u, obj.LastObservedRobotTimestamp);
        Assert.Equal(500.0, obj.Pose!.Position.X);
        Assert.True(located[0].Updated.SetEquals(new[] { "pose", "last_observed_robot_timestamp" }));
        Assert.Same(obj, located[0].Obj);
    }

    [Fact]
    public async Task LocatedStateHandlesInvalidAndDirtyPoses()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var obj = new ObservableObject(connection, timeProvider: clock);

        var located = new List<EvtObjectLocated>();
        obj.AddHandler<EvtObjectLocated>(located.Add);

        await obj.HandleLocatedObjectState(LocatedMessage(
            lastObservedTimestamp: 1,
            x: 5.0f,
            poseState: (byte)PoseState.Known));
        Assert.True(obj.Pose!.IsValid);
        Assert.True(obj.Pose.IsAccurate);

        await obj.HandleLocatedObjectState(LocatedMessage(
            lastObservedTimestamp: 2,
            x: 6.0f,
            poseState: (byte)PoseState.Invalid));
        Assert.False(obj.Pose!.IsValid);
        Assert.False(obj.Pose.IsAccurate);

        await obj.HandleLocatedObjectState(LocatedMessage(
            lastObservedTimestamp: 3,
            x: 7.0f,
            poseState: (byte)PoseState.Dirty));
        Assert.True(obj.Pose!.IsValid);
        Assert.False(obj.Pose.IsAccurate);
        Assert.Equal(7.0, obj.Pose.Position.X);
        Assert.Equal(3, located.Count);
    }

    [Fact]
    public async Task CubeHandlesTapMovePowerAndConnectionState()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var cube = new LightCube(
            CustomObjectTypes.LightCube1Id,
            connection,
            objectId: 7,
            timeProvider: clock);

        Assert.Equal(ObjectType.Block_LIGHTCUBE1, cube.CubeId.Value);
        Assert.True(cube.Pickupable);
        Assert.True(cube.PlaceObjectsOnThis);
        Assert.Null(cube.BatteryVoltage);
        Assert.Null(cube.BatteryPercentage);
        Assert.Equal("Unknown", cube.BatteryStr);
        Assert.Contains("Block_LIGHTCUBE1", cube.DescriptiveName, StringComparison.Ordinal);
        Assert.Contains("id=7", cube.DescriptiveName, StringComparison.Ordinal);

        var taps = new List<EvtObjectTapped>();
        var moveStarted = new List<EvtObjectMovingStarted>();
        var moving = new List<EvtObjectMoving>();
        var moveStopped = new List<EvtObjectMovingStopped>();
        var connections = new List<EvtObjectConnectChanged>();
        cube.AddHandler<EvtObjectTapped>(taps.Add);
        cube.AddHandler<EvtObjectMovingStarted>(moveStarted.Add);
        cube.AddHandler<EvtObjectMoving>(moving.Add);
        cube.AddHandler<EvtObjectMovingStopped>(moveStopped.Add);
        cube.AddHandler<EvtObjectConnectChanged>(connections.Add);

        await cube.HandleObjectTapped(new ObjectTapped(
            timestamp: 123,
            objectID: 7,
            numTaps: 2,
            tapTime: 50,
            tapNeg: -3,
            tapPos: 7));

        Assert.Single(taps);
        Assert.Same(cube, taps[0].Obj);
        Assert.Equal(2, taps[0].TapCount);
        Assert.Equal(50, taps[0].TapDuration);
        Assert.Equal(10, taps[0].TapIntensity);
        Assert.Equal(123u, cube.LastTappedRobotTimestamp);
        Assert.Equal(clock.GetUtcNow(), cube.LastTappedTime);
        Assert.Equal(clock.GetUtcNow(), cube.LastEventTime);

        await cube.HandleObserved(ObservedMessage(timestamp: 130, x: 1.0f, objectId: 7));
        Assert.True(cube.Pose!.IsValid);

        await cube.HandleObjectMoved(new ObjectMoved(
            timestamp: 200,
            objectID: 7,
            accel: new ActiveAccel(1.0f, 2.0f, 3.0f),
            axisOfAccel: 0));

        Assert.True(cube.IsMoving);
        Assert.Single(moveStarted);
        Assert.Equal(1.0, moveStarted[0].Acceleration.X);
        Assert.Equal(200u, cube.LastMovedStartRobotTimestamp);
        Assert.False(cube.Pose!.IsValid);

        clock.Advance(TimeSpan.FromSeconds(1));
        await cube.HandleObjectMoved(new ObjectMoved(
            timestamp: 201,
            objectID: 7,
            accel: new ActiveAccel(4.0f, 5.0f, 6.0f),
            axisOfAccel: 0));

        Assert.Single(moving);
        Assert.Equal(1.0, moving[0].MoveDuration, precision: 6);
        Assert.Equal(4.0, moving[0].Acceleration.X);

        clock.Advance(TimeSpan.FromSeconds(2));
        await cube.HandleObjectStoppedMoving(new ObjectStoppedMoving(timestamp: 202, objectID: 7));

        Assert.False(cube.IsMoving);
        Assert.Single(moveStopped);
        Assert.Equal(3.0, moveStopped[0].MoveDuration, precision: 6);

        await cube.HandleObjectPowerLevel(new ObjectPowerLevel(
            objectID: 7,
            missedPackets: 0,
            batteryLevel: 125));
        Assert.Equal(1.25, cube.BatteryVoltage);
        Assert.Equal(50.0, cube.BatteryPercentage);
        Assert.Equal("50%", cube.BatteryStr);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Unknown", new LightCube(
            CustomObjectTypes.LightCube2Id,
            connection,
            objectId: 8,
            timeProvider: clock).BatteryStr);

        Assert.False(cube.IsConnected);
        await cube.HandleObjectConnectionState(new ObjectConnectionState(
            objectID: 7,
            factoryID: 0,
            object_type: (int)ObjectType.Block_LIGHTCUBE1,
            connected: true));
        Assert.True(cube.IsConnected);
        Assert.Single(connections);
        Assert.True(connections[0].Connected);

        await cube.HandleObjectConnectionState(new ObjectConnectionState(
            objectID: 7,
            factoryID: 0,
            object_type: (int)ObjectType.Block_LIGHTCUBE1,
            connected: true));
        Assert.Single(connections);

        await cube.HandleObjectConnectionState(new ObjectConnectionState(
            objectID: 7,
            factoryID: 0,
            object_type: (int)ObjectType.Block_LIGHTCUBE1,
            connected: false));
        Assert.False(cube.IsConnected);
        Assert.Equal(2, connections.Count);
        Assert.False(connections[1].Connected);
    }

    [Fact]
    public async Task WaitForTapReturnsTheNextTap()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var cube = new LightCube(CustomObjectTypes.LightCube1Id, connection, objectId: 7);

        Task<EvtObjectTapped> wait = cube.WaitForTapAsync(TestTimeout);
        await cube.HandleObjectTapped(new ObjectTapped(
            timestamp: 1,
            objectID: 7,
            numTaps: 1,
            tapTime: 10,
            tapNeg: 0,
            tapPos: 4));

        EvtObjectTapped evt = await wait;
        Assert.Equal(1, evt.TapCount);
        Assert.Equal(4, evt.TapIntensity);
    }

    [Fact]
    public async Task SetLightsBuildsSetAllActiveObjectLedsMessage()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var cube = new LightCube(CustomObjectTypes.LightCube1Id, connection, objectId: 42);

        await cube.SetLightsAsync(Lights.Green);

        MessageGameToEngine sent = DecodeGameToEngine(Assert.Single(transport.SentFrames));
        Assert.Equal(MessageGameToEngine.Tag.SetAllActiveObjectLEDs, sent.tag);
        SetAllActiveObjectLEDs message = sent.SetAllActiveObjectLEDs;
        Assert.Equal(42u, message.objectID);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(Colors.Green.IntColor, message.onColor[index]);
            Assert.Equal(Colors.Green.IntColor, message.offColor[index]);
            Assert.Equal(Lights.Green.OnPeriodMs, message.onPeriod_ms[index]);
            Assert.Equal(Lights.Green.OffPeriodMs, message.offPeriod_ms[index]);
            Assert.Equal(Lights.Green.TransitionOnPeriodMs, message.transitionOnPeriod_ms[index]);
            Assert.Equal(Lights.Green.TransitionOffPeriodMs, message.transitionOffPeriod_ms[index]);
        }
    }

    [Fact]
    public async Task SetLightCornersSkipsNullSlots()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var cube = new LightCube(CustomObjectTypes.LightCube2Id, connection, objectId: 43);

        await cube.SetLightCornersAsync(Lights.Red, null, Lights.Blue, Lights.Off);

        SetAllActiveObjectLEDs message = DecodeGameToEngine(Assert.Single(transport.SentFrames))
            .SetAllActiveObjectLEDs;
        Assert.Equal(43u, message.objectID);
        Assert.Equal(Colors.Red.IntColor, message.onColor[0]);
        Assert.Equal(0u, message.onColor[1]);
        Assert.Equal(Colors.Blue.IntColor, message.onColor[2]);
        Assert.Equal(0u, message.onColor[3]);
        Assert.Equal(Colors.Red.IntColor, message.offColor[0]);
        Assert.Equal(Colors.Blue.IntColor, message.offColor[2]);
    }

    [Fact]
    public async Task SetLightsListAcceptsOneOrFourLights()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var cube = new LightCube(CustomObjectTypes.LightCube3Id, connection, objectId: 44);

        await cube.SetLightsAsync(new[] { Lights.Red, Lights.Green, Lights.Blue, Lights.White });
        SetAllActiveObjectLEDs corners = DecodeGameToEngine(transport.SentFrames[0]).SetAllActiveObjectLEDs;
        Assert.Equal(Colors.Red.IntColor, corners.onColor[0]);
        Assert.Equal(Colors.Green.IntColor, corners.onColor[1]);
        Assert.Equal(Colors.Blue.IntColor, corners.onColor[2]);
        Assert.Equal(Colors.White.IntColor, corners.onColor[3]);

        await cube.SetLightsAsync(new[] { Lights.White });
        SetAllActiveObjectLEDs all = DecodeGameToEngine(transport.SentFrames[1]).SetAllActiveObjectLEDs;
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(Colors.White.IntColor, all.onColor[index]);
        }

        Assert.Throws<ArgumentException>(() =>
        {
            _ = cube.SetLightsAsync(new[] { Lights.Red, Lights.Green });
        });
    }

    [Fact]
    public async Task SetLightsOffSendsOffLight()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var cube = new LightCube(CustomObjectTypes.LightCube1Id, connection, objectId: 45);

        await cube.SetLightsOffAsync();

        SetAllActiveObjectLEDs message = DecodeGameToEngine(Assert.Single(transport.SentFrames))
            .SetAllActiveObjectLEDs;
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(0u, message.onColor[index]);
            Assert.Equal(0u, message.offColor[index]);
        }
    }

    [Fact]
    public async Task SetLightsRequiresAnObjectId()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var cube = new LightCube(CustomObjectTypes.LightCube1Id, connection);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = cube.SetLightsAsync(Lights.Green);
        });
        Assert.Empty(transport.SentFrames);
    }

    [Fact]
    public void CustomObjectCataloguesMatchTheCladEnums()
    {
        // 24 catalogue entries plus the LightCubeIDs list field.
        Assert.Equal(25, typeof(CustomObjectTypes).GetFields(BindingFlags.Public | BindingFlags.Static).Length);
        Assert.Equal(3, CustomObjectTypes.LightCubeIDs.Count);
        for (int index = 0; index < 20; index++)
        {
            string name = $"CustomType{index:D2}";
            FieldInfo field = typeof(CustomObjectTypes).GetField(name, BindingFlags.Public | BindingFlags.Static)!;
            var entry = (CladEnum<ObjectType>)field.GetValue(null)!;

            Assert.Equal((ObjectType)(17 + index), entry.Value);
            Assert.Equal(name, entry.Name);
            Assert.Equal(17 + index, entry.Id);
        }

        Assert.Equal(ObjectType.Block_LIGHTCUBE1, CustomObjectTypes.LightCube1Id.Value);
        Assert.Equal(ObjectType.Block_LIGHTCUBE2, CustomObjectTypes.LightCube2Id.Value);
        Assert.Equal(ObjectType.Block_LIGHTCUBE3, CustomObjectTypes.LightCube3Id.Value);
        Assert.Equal(ObjectType.Charger_Basic, CustomObjectTypes.ChargerBasic.Value);
        Assert.Equal(
            new[]
            {
                CustomObjectTypes.LightCube1Id,
                CustomObjectTypes.LightCube2Id,
                CustomObjectTypes.LightCube3Id,
            },
            CustomObjectTypes.LightCubeIDs.ToArray());

        Assert.Equal(16, typeof(CustomObjectMarkers).GetFields(BindingFlags.Public | BindingFlags.Static).Length);
        Array markerValues = Enum.GetValues<CustomObjectMarker>();
        for (int index = 0; index < 16; index++)
        {
            string name = markerValues.GetValue(index)!.ToString()!;
            FieldInfo field = typeof(CustomObjectMarkers).GetField(name, BindingFlags.Public | BindingFlags.Static)!;
            var entry = (CladEnum<CustomObjectMarker>)field.GetValue(null)!;

            Assert.Equal((CustomObjectMarker)index, entry.Value);
            Assert.Equal(name, entry.Name);
            Assert.Equal(index, entry.Id);
        }

        Assert.Null(typeof(CustomObjectMarkers).GetField("Count", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public async Task CustomObjectExposesSizesAndType()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var custom = new CustomObject(
            connection,
            world: null,
            CustomObjectTypes.CustomType07,
            xSizeMm: 10.0,
            ySizeMm: 20.0,
            zSizeMm: 30.0,
            markerWidthMm: 40.0,
            markerHeightMm: 50.0,
            isUnique: true,
            objectId: 12);

        Assert.Equal(ObjectType.CustomType07, custom.ObjectType.Value);
        Assert.Equal(10.0, custom.XSizeMm);
        Assert.Equal(20.0, custom.YSizeMm);
        Assert.Equal(30.0, custom.ZSizeMm);
        Assert.Equal(40.0, custom.MarkerWidthMm);
        Assert.Equal(50.0, custom.MarkerHeightMm);
        Assert.True(custom.IsUnique);
        Assert.False(custom.Pickupable);
        Assert.False(custom.PlaceObjectsOnThis);
        Assert.Equal("CustomType07 id=12", custom.DescriptiveName);
    }

    [Fact]
    public void FixedCustomObjectTracksDefinitionState()
    {
        Pose pose = Pose.FromZAngle(1.0, 2.0, 3.0, Angle.FromDegrees(45.0), originId: 5);
        var fixedObject = new FixedCustomObject(pose, xSizeMm: 10.0, ySizeMm: 20.0, zSizeMm: 30.0);

        Assert.False(fixedObject.IsVisible);
        Assert.False(fixedObject.IsDefined);
        Assert.Null(fixedObject.ObjectId);
        Assert.Same(pose, fixedObject.Pose);
        Assert.Equal(10.0, fixedObject.XSizeMm);
        Assert.Equal(20.0, fixedObject.YSizeMm);
        Assert.Equal(30.0, fixedObject.ZSizeMm);
        Assert.Contains("FixedCustomObject", fixedObject.ToString(), StringComparison.Ordinal);

        fixedObject.ObjectId = 99;
        Assert.True(fixedObject.IsDefined);
        Assert.Equal(99, fixedObject.ObjectId);

        Assert.Throws<InvalidOperationException>(() => fixedObject.ObjectId = 100);
        Assert.Equal(99, fixedObject.ObjectId);
    }

    [Fact]
    public async Task ObjectIdCanBeReassignedWithoutThrowing()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var obj = new ObservableObject(connection);

        Assert.Null(obj.ObjectId);
        Assert.Equal("ObservableObject id=None", obj.DescriptiveName);

        obj.ObjectId = 1;
        obj.ObjectId = 2;
        Assert.Equal(2, obj.ObjectId);
        Assert.Equal("ObservableObject id=2", obj.DescriptiveName);
    }

    private static RobotObservedObject ObservedMessage(
        uint timestamp,
        float x,
        int objectId = 7) => new()
        {
            timestamp = timestamp,
            objectID = objectId,
            objectFamily = (int)ObjectFamily.LightCube,
            objectType = (int)ObjectType.Block_LIGHTCUBE1,
            pose = new PoseStruct3d(x, 2.0f, 3.0f, 1.0f, 0.0f, 0.0f, 0.0f, 5),
            img_rect = new CladRect(1.0f, 2.0f, 3.0f, 4.0f),
        };

    private static LocatedObjectState LocatedMessage(
        uint lastObservedTimestamp,
        float x,
        byte poseState = (byte)PoseState.Known,
        int objectId = 7) => new()
        {
            objectID = (uint)objectId,
            lastObservedTimestamp = lastObservedTimestamp,
            objectFamily = (int)ObjectFamily.LightCube,
            objectType = (int)ObjectType.Block_LIGHTCUBE1,
            pose = new PoseStruct3d(x, 2.0f, 3.0f, 1.0f, 0.0f, 0.0f, 0.0f, 5),
            poseState = poseState,
            isConnected = true,
        };

    private static MessageGameToEngine DecodeGameToEngine(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
    }

    /// <summary>Manual clock used to drive visibility timers deterministically.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = new();
        private DateTimeOffset _utcNow = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan delta)
        {
            List<ManualTimer> due;
            lock (_gate)
            {
                _utcNow += delta;
                due = _timers
                    .Where(timer => timer.IsDue(_utcNow))
                    .OrderBy(timer => timer.DueAt)
                    .ToList();
            }

            foreach (ManualTimer timer in due)
            {
                timer.Fire();
            }
        }

        private void RemoveTimer(ManualTimer timer)
        {
            lock (_gate)
            {
                _timers.Remove(timer);
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private bool _disposed;
            private TimeSpan _period;

            public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
            }

            public DateTimeOffset? DueAt { get; private set; }

            public bool IsDue(DateTimeOffset now) =>
                !_disposed && DueAt is DateTimeOffset dueAt && dueAt <= now;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed)
                {
                    return false;
                }

                _period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : _owner.GetUtcNow() + dueTime;
                return true;
            }

            public void Fire()
            {
                if (_disposed)
                {
                    return;
                }

                DueAt = _period == Timeout.InfiniteTimeSpan
                    ? null
                    : _owner.GetUtcNow() + _period;
                _callback(_state);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                DueAt = null;
                _owner.RemoveTimer(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
