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

using Anki.Cozmo;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;
using Xunit;

namespace Cozmo.Tests;

public sealed class RobotTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task RobotStateUpdatesProperties()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtRobotStateUpdated> updated = harness.Robot.WaitFor<EvtRobotStateUpdated>(timeout: TestTimeout);

        harness.Enqueue(new MessageEngineToGame
        {
            RobotState = new Anki.Cozmo.ExternalInterface.RobotState
            {
                pose = Pose.FromZAngle(10, 20, 0, Util.Degrees(30)).EncodePose(),
                poseAngle_rad = (float)Util.Degrees(30).Radians,
                posePitch_rad = 0,
                headAngle_rad = (float)Util.Degrees(10).Radians,
                liftHeight_mm = 62,
                batteryVoltage = 4.0f,
                leftWheelSpeed_mmps = 5,
                rightWheelSpeed_mmps = -5,
                accel = new Anki.Cozmo.AccelData { x = 1, y = 2, z = 3 },
                gyro = new Anki.Cozmo.GyroData { x = 4, y = 5, z = 6 },
                carryingObjectID = 12,
                status = (uint)(RobotStatusFlag.IS_MOVING | RobotStatusFlag.IS_CHARGING),
                gameStatus = (byte)GameStatusFlag.IsLocalized,
            },
        });

        await updated;
        Assert.Equal(10, harness.Robot.Pose!.Position.X, 3);
        Assert.Equal(20, harness.Robot.Pose.Position.Y, 3);
        Assert.Equal(10, harness.Robot.HeadAngle!.Value.Degrees, 3);
        Assert.Equal(62, harness.Robot.LiftHeight.DistanceMillimeters, 3);
        Assert.Equal(0.5, harness.Robot.LiftRatio, 3);
        Assert.Equal(4.0f, harness.Robot.BatteryVoltage);
        Assert.Equal(5, harness.Robot.LeftWheelSpeed!.Value.SpeedMmps, 3);
        Assert.Equal(12, harness.Robot.CarryingObjectId);
        Assert.True(harness.Robot.IsMoving);
        Assert.True(harness.Robot.IsCharging);
        Assert.False(harness.Robot.IsOnCharger);
        Assert.True(harness.Robot.IsLocalized);
        Vector3 accel = Assert.IsType<Vector3>(harness.Robot.Accelerometer);
        Assert.Equal(1, accel.X, 3);
        Assert.Equal(2, accel.Y, 3);
        Assert.Equal(3, accel.Z, 3);
    }

    [Fact]
    public async Task InitializeSendsSetupCommandsAndRaisesReady()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtRobotReady> ready = harness.Robot.WaitFor<EvtRobotReady>(timeout: TestTimeout);

        Task initialization = harness.Robot.InitializeAsync();

        await initialization.WaitAsync(TestTimeout);
        await ready;

        Assert.True(harness.Robot.IsReady);
        Assert.True(harness.Robot.AnimNames.IsLoaded);
        List<MessageGameToEngine> sent = harness.Transport.SentFrames
            .Select(Decode)
            .ToList();
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.StopAllMotors);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.RequestRobotSettings);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.DisableAllReactionsWithLock);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.EnableStopOnCliff);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.SetMemoryMapBroadcastFrequency_sec);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.EnableDeviceIMUData);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.EnableLightStates);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.GetBlockPoolMessage);
        Assert.Contains(sent, message => message.tag == MessageGameToEngine.Tag.PushIdleAnimation);

        MessageGameToEngine behavior = sent.First(
            message => message.tag == MessageGameToEngine.Tag.ExecuteBehaviorByExecutableType);
        Assert.Equal(-1, behavior.ExecuteBehaviorByExecutableType.numRuns);
    }

    [Fact]
    public async Task SayTextSendsAnAction()
    {
        await using Harness harness = await Harness.CreateAsync();

        Anki.Cozmo.Sdk.SayText action = await harness.Robot.SayTextAsync("hello", useCozmoVoice: true);

        Assert.True(action.IsRunning);
        MessageGameToEngine sent = LastSent(harness);
        Assert.Equal(MessageGameToEngine.Tag.QueueSingleAction, sent.tag);
        Assert.Equal("hello", sent.QueueSingleAction.action.sayText.text);
        Assert.Equal((byte)SayTextVoiceStyle.CozmoProcessing_Sentence,
            sent.QueueSingleAction.action.sayText.voiceStyle);
    }

    [Fact]
    public async Task PlayAnimationRequiresLoadedNamesAndKnownName()
    {
        await using Harness harness = await Harness.CreateAsync();

        await Assert.ThrowsAsync<AnimationsNotLoadedException>(
            () => harness.Robot.PlayAnimationAsync("anim_a"));

        harness.Enqueue(new MessageEngineToGame
        {
            AnimationAvailable = new AnimationAvailable { animName = "anim_a" },
        });
        harness.Enqueue(new MessageEngineToGame { EndOfMessage = new EndOfMessage() });
        await harness.Robot.AnimNames.WaitForLoadedAsync(TestTimeout);

        Animation action = await harness.Robot.PlayAnimationAsync("anim_a", loopCount: 2);
        Assert.True(action.IsRunning);
        Assert.Equal("anim_a", action.AnimName);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Robot.PlayAnimationAsync("nope"));
    }

    [Fact]
    public async Task SetBackpackLightsFillsAllFiveSlots()
    {
        await using Harness harness = await Harness.CreateAsync();

        await harness.Robot.SetAllBackpackLightsAsync(Lights.Green);

        MessageGameToEngine sent = LastSent(harness);
        Assert.Equal(MessageGameToEngine.Tag.SetBackpackLEDs, sent.tag);
        SetBackpackLEDs message = sent.SetBackpackLEDs;
        Assert.All(message.onColor, value => Assert.Equal(Colors.Green.IntColor, value));
        Assert.All(message.offColor, value => Assert.Equal(Colors.Green.IntColor, value));
    }

    [Fact]
    public async Task SetHeadAngleClampsToSupportedRange()
    {
        await using Harness harness = await Harness.CreateAsync();

        Anki.Cozmo.Sdk.SetHeadAngle action = await harness.Robot.SetHeadAngleAsync(
            Util.Degrees(-40),
            warnOnClamp: false);

        Assert.Equal(-25, action.Angle.Degrees, 3);
        MessageGameToEngine sent = LastSent(harness);
        Assert.Equal((float)Util.Degrees(-25).Radians, sent.QueueSingleAction.action.setHeadAngle.angle_rad, 4);
    }

    [Fact]
    public async Task StartBehaviorSendsExecuteAndRequestsBehavior()
    {
        await using Harness harness = await Harness.CreateAsync();

        Behavior behavior = await harness.Robot.StartBehaviorAsync(BehaviorTypes.LookAroundInPlace);

        Assert.True(behavior.IsActive);
        Assert.Same(behavior, harness.Robot.CurrentBehavior);
        MessageGameToEngine sent = LastSent(harness);
        Assert.Equal(MessageGameToEngine.Tag.ExecuteBehaviorByExecutableType, sent.tag);
        Assert.Equal((byte)BehaviorTypes.LookAroundInPlace.Id, sent.ExecuteBehaviorByExecutableType.behaviorType);
        Assert.Equal(-1, sent.ExecuteBehaviorByExecutableType.numRuns);
    }

    [Fact]
    public async Task PickupRejectsNonPickupableObject()
    {
        await using Harness harness = await Harness.CreateAsync();
        var charger = new Charger(harness.Connection, objectId: 5);

        await Assert.ThrowsAsync<NotPickupableException>(
            () => harness.Robot.PickupObjectAsync(charger));
    }

    [Fact]
    public async Task AbortAllActionsSendsWildcardCancel()
    {
        await using Harness harness = await Harness.CreateAsync();
        await harness.Robot.ActionDispatcher.AbortAllActionsAsync();

        await WaitUntilAsync(() => harness.Transport.SentFrames.Count > 0);
        Assert.Contains(
            harness.Transport.SentFrames.Select(Decode),
            message => message.tag == MessageGameToEngine.Tag.CancelAction);
    }

    private static MessageGameToEngine LastSent(Harness harness)
    {
        Assert.NotEmpty(harness.Transport.SentFrames);
        return Decode(harness.Transport.SentFrames[^1]);
    }

    private static MessageGameToEngine Decode(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TestTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(5);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private Harness()
        {
            Transport = new FakeTransport();
            Connection = new CozmoConnection(Transport);
            Transport.FrameSent += OnFrameSent;
            Robot = new Robot(Connection, robotId: 1, isPrimary: true, autoInitialize: false);
        }

        public FakeTransport Transport { get; }

        public CozmoConnection Connection { get; }

        public Robot Robot { get; }

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();
            await harness.Connection.ConnectAsync();
            return harness;
        }

        private void OnFrameSent(byte[] payload)
        {
            MessageGameToEngine message;
            using (var stream = new MemoryStream(payload, writable: false))
            {
                message = MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
            }

            switch (message.tag)
            {
                case MessageGameToEngine.Tag.DeleteAllCustomObjects:
                    Enqueue(new MessageEngineToGame
                    {
                        RobotDeletedAllCustomObjects = new RobotDeletedAllCustomObjects(),
                    });
                    break;
                case MessageGameToEngine.Tag.RequestAvailableAnimations:
                    Enqueue(new MessageEngineToGame
                    {
                        AnimationAvailable = new AnimationAvailable { animName = "anim_a" },
                    });
                    Enqueue(new MessageEngineToGame { EndOfMessage = new EndOfMessage() });
                    break;
            }
        }

        public void Enqueue(MessageEngineToGame message)
        {
            using var stream = new MemoryStream();
            message.Pack(new CladBinaryWriter(stream));
            Transport.EnqueueFrame(stream.ToArray());
        }

        public async ValueTask DisposeAsync()
        {
            Transport.FrameSent -= OnFrameSent;
            Robot.Dispose();
            await Connection.DisposeAsync();
            await Transport.DisposeAsync();
        }
    }
}
