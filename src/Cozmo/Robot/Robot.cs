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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CladAnimationTrigger = Anki.Cozmo.AnimationTrigger;
using CladMovementSide = Anki.Cozmo.UnexpectedMovementSide;
using CladMovementType = Anki.Cozmo.UnexpectedMovementType;

namespace Anki.Cozmo.Sdk;

/// <summary>The interface to a Cozmo robot.</summary>
/// <remarks>
/// <para>
/// Port of <c>robot.py::Robot</c>. A robot owns the connection's action dispatcher, the
/// <see cref="Cozmo.Sdk.World"/>, the <see cref="Cozmo.Sdk.Camera"/> and the animation-name
/// cache, handles inbound robot state, and exposes the high-level commands.
/// </para>
/// <para>
/// Creation normally happens in <c>CozmoRunner</c> after the connection handshake; construction
/// starts <see cref="InitializeAsync"/> unless <c>autoInitialize</c> is false (tests).
/// </para>
/// </remarks>
public sealed class Robot : Dispatcher, IWorldRobot
{
    private readonly CozmoConnection _connection;
    private readonly ILogger<Robot> _logger;
    private readonly Handler _messageHandler;
    private readonly Handler _closedHandler;
    private readonly ActionDispatcher _actionDispatcher;
    private readonly global::Anki.Cozmo.Sdk.Camera _camera;
    private readonly global::Anki.Cozmo.Sdk.World _world;
    private readonly AnimationNames _animNames;
    private Pose? _pose;
    private Angle? _poseAngle;
    private Angle? _posePitch;
    private Angle? _headAngle;
    private Speed? _leftWheelSpeed;
    private Speed? _rightWheelSpeed;
    private global::Anki.Cozmo.Sdk.LiftPosition _liftPosition =
        global::Anki.Cozmo.Sdk.LiftPosition.FromHeight(
            Distance.FromMillimeters(global::Anki.Cozmo.Sdk.LiftPosition.MinLiftHeightMillimeters));
    private float? _batteryVoltage;
    private Vector3? _accelerometer;
    private Vector3? _gyro;
    private int _carryingObjectId = -1;
    private int _carryingObjectOnTopId = -1;
    private int _headTrackingObjectId = -1;
    private int _localizedToObjectId = -1;
    private uint? _lastImageRobotTimestamp;
    private uint _robotStatusFlags;
    private byte _gameStatusFlags;
    private uint _serialNumberHead;
    private uint _serialNumberBody;
    private uint _modelNumber;
    private int _hwVersion;
    private bool? _isDeviceAccelerometerSupported;
    private bool? _isDeviceGyroSupported;
    private Vector3? _deviceAccelRaw;
    private Vector3? _deviceAccelUser;
    private Quaternion? _deviceGyro;
    private Behavior? _currentBehavior;
    private DisplayOledFaceImage? _currentFaceImageAction;
    private bool _isReady;
    private bool _isFreeplayModeActive;
    private int _idleStackDepth;
    private int _initializationStarted;

    /// <summary>Creates a robot for a connection.</summary>
    /// <param name="connection">The active connection.</param>
    /// <param name="robotId">The engine's id for the robot.</param>
    /// <param name="isPrimary">True for the primary robot.</param>
    /// <param name="timeProvider">Optional clock; used by the world and its timers.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="autoInitialize">False to skip the automatic initialization sequence (tests).</param>
    public Robot(
        CozmoConnection connection,
        int robotId,
        bool isPrimary,
        TimeProvider? timeProvider = null,
        ILogger<Robot>? logger = null,
        bool autoInitialize = true)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        RobotId = robotId;
        IsPrimary = isPrimary;
        _logger = logger ?? NullLogger<Robot>.Instance;

        _camera = new global::Anki.Cozmo.Sdk.Camera(connection);
        _world = new global::Anki.Cozmo.Sdk.World(connection, this, timeProvider);
        _actionDispatcher = new global::Anki.Cozmo.Sdk.ActionDispatcher(connection);
        _animNames = new AnimationNames(connection);

        AddChildDispatcher(_camera);
        AddChildDispatcher(_actionDispatcher);
        AddChildDispatcher(_world);

        _messageHandler = connection.AddAsyncHandler<EvtMessageReceived>(OnMessageReceivedAsync);
        _closedHandler = connection.AddAsyncHandler<EvtConnectionClosed>(OnConnectionClosedAsync);

        if (autoInitialize)
        {
            InitializationTask = InitializeAsync();
        }
    }

    /// <inheritdoc />
    public CozmoConnection Connection => _connection;

    /// <inheritdoc />
    public int RobotId { get; }

    /// <summary>True when this is the primary robot.</summary>
    public bool IsPrimary { get; }

    /// <inheritdoc />
    public Pose? Pose => _pose;

    /// <summary>The robot's camera.</summary>
    public global::Anki.Cozmo.Sdk.Camera Camera => _camera;

    /// <summary>The world this robot perceives.</summary>
    public global::Anki.Cozmo.Sdk.World World => _world;

    /// <summary>The dispatcher that sends and tracks actions.</summary>
    public ActionDispatcher ActionDispatcher => _actionDispatcher;

    /// <summary>The animation names reported by the engine.</summary>
    public AnimationNames AnimNames => _animNames;

    /// <summary>The animation triggers the SDK knows about.</summary>
    public IReadOnlyList<CladEnum<CladAnimationTrigger>> AnimTriggers => Triggers.TriggerList;

    /// <summary>The initialization task, or null when auto-initialization was disabled.</summary>
    public Task? InitializationTask { get; }

    /// <summary>True once initialization has completed and the robot accepts commands.</summary>
    public bool IsReady => _isReady;

    /// <summary>Heading in the X-Y plane.</summary>
    public Angle? PoseAngle => _poseAngle;

    /// <summary>Pitch of the robot.</summary>
    public Angle? PosePitch => _posePitch;

    /// <summary>Head angle.</summary>
    public Angle? HeadAngle => _headAngle;

    /// <summary>Lift position.</summary>
    public global::Anki.Cozmo.Sdk.LiftPosition LiftPosition => _liftPosition;

    /// <summary>Lift height above the ground.</summary>
    public Distance LiftHeight => _liftPosition.Height;

    /// <summary>Lift height as a 0..1 ratio.</summary>
    public double LiftRatio => _liftPosition.Ratio;

    /// <summary>Lift arm angle relative to the ground.</summary>
    public Angle LiftAngle => _liftPosition.Angle;

    /// <summary>Speed of the left wheel.</summary>
    public Speed? LeftWheelSpeed => _leftWheelSpeed;

    /// <summary>Speed of the right wheel.</summary>
    public Speed? RightWheelSpeed => _rightWheelSpeed;

    /// <summary>Current battery voltage.</summary>
    public float? BatteryVoltage => _batteryVoltage;

    /// <summary>Current accelerometer reading, in mm/s^2.</summary>
    public Vector3? Accelerometer => _accelerometer;

    /// <summary>Current gyro reading, in radians/s.</summary>
    public Vector3? Gyro => _gyro;

    /// <summary>Id of the object being carried, or -1.</summary>
    public int CarryingObjectId => _carryingObjectId;

    /// <summary>Id of the object on top of the carried object, or -1.</summary>
    public int CarryingObjectOnTopId => _carryingObjectOnTopId;

    /// <summary>Id of the object the head is tracking, or -1.</summary>
    public int HeadTrackingObjectId => _headTrackingObjectId;

    /// <summary>Id of the object the robot is localized to, or -1.</summary>
    public int LocalizedToObjectId => _localizedToObjectId;

    /// <summary>Robot timestamp of the last image, or null.</summary>
    public uint? LastImageRobotTimestamp => _lastImageRobotTimestamp;

    /// <summary>True when the robot is moving.</summary>
    public bool IsMoving => HasStatus(RobotStatusFlag.IS_MOVING);

    /// <summary>True when the robot is carrying an object.</summary>
    public bool IsCarryingBlock => HasStatus(RobotStatusFlag.IS_CARRYING_BLOCK);

    /// <summary>True when the robot is picking up or placing an object.</summary>
    public bool IsPickingOrPlacing => HasStatus(RobotStatusFlag.IS_PICKING_OR_PLACING);

    /// <summary>True when the robot has been picked up.</summary>
    public bool IsPickedUp => HasStatus(RobotStatusFlag.IS_PICKED_UP);

    /// <summary>True when the robot is falling.</summary>
    public bool IsFalling => HasStatus(RobotStatusFlag.IS_FALLING);

    /// <summary>True when the robot is playing an animation.</summary>
    public bool IsAnimating => HasStatus(RobotStatusFlag.IS_ANIMATING);

    /// <summary>True when the robot is playing an idle animation.</summary>
    public bool IsAnimatingIdle => HasStatus(RobotStatusFlag.IS_ANIMATING_IDLE);

    /// <summary>True when the robot is following a path.</summary>
    public bool IsPathing => HasStatus(RobotStatusFlag.IS_PATHING);

    /// <summary>True when the lift is at a requested position.</summary>
    public bool IsLiftInPosition => HasStatus(RobotStatusFlag.LIFT_IN_POS);

    /// <summary>True when the head is at a requested position.</summary>
    public bool IsHeadInPosition => HasStatus(RobotStatusFlag.HEAD_IN_POS);

    /// <summary>True when the animation buffer is full.</summary>
    public bool IsAnimBufferFull => HasStatus(RobotStatusFlag.IS_ANIM_BUFFER_FULL);

    /// <summary>True when the robot is on the charger.</summary>
    public bool IsOnCharger => HasStatus(RobotStatusFlag.IS_ON_CHARGER);

    /// <summary>True when the battery is charging.</summary>
    public bool IsCharging => HasStatus(RobotStatusFlag.IS_CHARGING);

    /// <summary>True when a cliff is detected.</summary>
    public bool IsCliffDetected => HasStatus(RobotStatusFlag.CLIFF_DETECTED);

    /// <summary>True when the wheels are moving.</summary>
    public bool AreWheelsMoving => HasStatus(RobotStatusFlag.ARE_WHEELS_MOVING);

    /// <summary>True when the robot is localized.</summary>
    public bool IsLocalized => (_gameStatusFlags & (byte)GameStatusFlag.IsLocalized) != 0;

    /// <summary>The behavior currently active, or null.</summary>
    public Behavior? CurrentBehavior => _currentBehavior is { IsActive: true } ? _currentBehavior : null;

    /// <summary>True when a behavior is running, either explicitly or through freeplay.</summary>
    public bool IsBehaviorRunning => IsFreeplayModeActive || CurrentBehavior is not null;

    /// <summary>True while freeplay mode is active.</summary>
    public bool IsFreeplayModeActive => _isFreeplayModeActive;

    /// <summary>True when at least one action is in progress.</summary>
    public bool HasInProgressActions => _actionDispatcher.HasInProgressActions;

    /// <summary>The robot's serial number, as eight lowercase hex digits.</summary>
    public string Serial => _serialNumberBody.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The robot's model number.</summary>
    public uint ModelNumber => _modelNumber;

    /// <summary>The robot's hardware version.</summary>
    public int HardwareVersion => _hwVersion;

    /// <summary>True when the connected device supports accelerometer data.</summary>
    public bool? IsDeviceAccelerometerSupported => _isDeviceAccelerometerSupported;

    /// <summary>True when the connected device supports gyro data.</summary>
    public bool? IsDeviceGyroSupported => _isDeviceGyroSupported;

    /// <summary>Raw accelerometer data from the connected device.</summary>
    public Vector3? DeviceAccelRaw => _deviceAccelRaw;

    /// <summary>User-filtered accelerometer data from the connected device.</summary>
    public Vector3? DeviceAccelUser => _deviceAccelUser;

    /// <summary>Gyro data from the connected device.</summary>
    public Quaternion? DeviceGyro => _deviceGyro;

    /// <summary>The camera configuration.</summary>
    public CameraConfig? CameraConfig => _camera.Config;

    /// <summary>Initializes the robot and raises <see cref="EvtRobotReady"/> when complete.</summary>
    public async Task InitializeAsync()
    {
        if (Interlocked.Exchange(ref _initializationStarted, 1) != 0)
        {
            return;
        }

        try
        {
            // The engine pushes PerRobotSettings exactly once, right after the SDK handshake.
            // The runner creates the robot immediately afterwards, so that first message can be
            // dispatched before this robot subscribes; ask again now that the handlers are wired.
            await SendAsync(new MessageGameToEngine { RequestRobotSettings = new RequestRobotSettings() })
                .ConfigureAwait(false);

            // robot.py:846-885 - the robot's state is reset on entering SDK mode.
            await StopAllMotorsAsync().ConfigureAwait(false);
            await EnableAllReactionTriggersAsync(false).ConfigureAwait(false);
            await EnableStopOnCliffAsync(true).ConfigureAwait(false);
            await SetNoneBehaviorAsync().ConfigureAwait(false);
            await _world.RequestNavMemoryMapAsync(-1.0).ConfigureAwait(false);
            await EnableDeviceImuAsync(false, false, false).ConfigureAwait(false);
            await SetCubeLightStateAsync(false).ConfigureAwait(false);

            await _world.DeleteAllCustomObjectsAsync().ConfigureAwait(false);
            await _animNames.RefreshAsync().ConfigureAwait(false);
            await _animNames.WaitForLoadedAsync().ConfigureAwait(false);

            await SendAsync(new MessageGameToEngine { GetBlockPoolMessage = new GetBlockPoolMessage() })
                .ConfigureAwait(false);

            _isReady = true;
            _logger.LogInformation("Robot id={RobotId} serial={Serial} initialized OK.", RobotId, Serial);
            await DispatchEventAsync(new EvtRobotReady(this)).ConfigureAwait(false);

            _idleStackDepth = 0;
            await SetIdleAnimationAsync(Triggers.Count).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Robot id={RobotId} initialization failed.", RobotId);
            throw;
        }
    }

    internal async Task SetNoneBehaviorAsync()
    {
        await SendAsync(new MessageGameToEngine
        {
            ExecuteBehaviorByExecutableType = new ExecuteBehaviorByExecutableType
            {
                behaviorType = (byte)ExecutableBehaviorType.Wait,
                numRuns = -1,
            },
        }).ConfigureAwait(false);

        if (_currentBehavior is not null)
        {
            await _currentBehavior.NotifyStoppedAsync().ConfigureAwait(false);
        }
    }

    internal Task SetCubeLightStateAsync(bool enable) =>
        SendAsync(new MessageGameToEngine
        {
            EnableLightStates = new EnableLightStates { enable = enable, objectID = -1 },
        });

    internal Task EnableCubeSleepAsync(bool enable = true, bool skipAnimation = true) =>
        SendAsync(new MessageGameToEngine
        {
            EnableCubeSleep = new EnableCubeSleep { enable = enable, skipAnimation = skipAnimation },
        });

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _messageHandler.Dispose();
            _closedHandler.Dispose();
            _currentBehavior?.Dispose();
            _actionDispatcher.Dispose();
            _world.Dispose();
            _camera.Dispose();
            _animNames.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool HasStatus(RobotStatusFlag flag) => (_robotStatusFlags & (uint)flag) != 0;

    private Task SendAsync(MessageGameToEngine message) => _connection.SendAsync(message);

    private Task OnConnectionClosedAsync(EvtConnectionClosed evt)
    {
        _logger.LogInformation("Robot id={RobotId} connection closed: {Reason}.", RobotId, evt.Reason.Message);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(EvtMessageReceived evt)
    {
        MessageEngineToGame message = evt.Message;
        switch (message.tag)
        {
            case MessageEngineToGame.Tag.RobotState:
                await OnRobotStateAsync(message.RobotState).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.PerRobotSettings:
                OnPerRobotSettings(message.PerRobotSettings);
                break;
            case MessageEngineToGame.Tag.BehaviorTransition:
                await OnBehaviorTransitionAsync(message.BehaviorTransition).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.UnexpectedMovement:
                await OnUnexpectedMovementAsync(message.UnexpectedMovement).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.IsDeviceIMUSupported:
                _isDeviceAccelerometerSupported = message.IsDeviceIMUSupported.isAccelerometerSupported;
                _isDeviceGyroSupported = message.IsDeviceIMUSupported.isGyroSupported;
                break;
            case MessageEngineToGame.Tag.DeviceAccelerometerValuesRaw:
                _deviceAccelRaw = new Vector3(
                    message.DeviceAccelerometerValuesRaw.x_gForce,
                    message.DeviceAccelerometerValuesRaw.y_gForce,
                    message.DeviceAccelerometerValuesRaw.z_gForce);
                break;
            case MessageEngineToGame.Tag.DeviceAccelerometerValuesUser:
                _deviceAccelUser = new Vector3(
                    message.DeviceAccelerometerValuesUser.x_gForce,
                    message.DeviceAccelerometerValuesUser.y_gForce,
                    message.DeviceAccelerometerValuesUser.z_gForce);
                break;
            case MessageEngineToGame.Tag.DeviceGyroValues:
                _deviceGyro = new Quaternion(
                    message.DeviceGyroValues.w,
                    message.DeviceGyroValues.x,
                    message.DeviceGyroValues.y,
                    message.DeviceGyroValues.z);
                break;
        }
    }

    private async Task OnRobotStateAsync(global::Anki.Cozmo.ExternalInterface.RobotState state)
    {
        _pose = Pose.FromClad(state.pose);
        _poseAngle = Angle.FromRadians(state.poseAngle_rad);
        _posePitch = Angle.FromRadians(state.posePitch_rad);
        _headAngle = Angle.FromRadians(state.headAngle_rad);
        _leftWheelSpeed = Speed.FromMillimetersPerSecond(state.leftWheelSpeed_mmps);
        _rightWheelSpeed = Speed.FromMillimetersPerSecond(state.rightWheelSpeed_mmps);
        _liftPosition = global::Anki.Cozmo.Sdk.LiftPosition.FromHeight(
            Distance.FromMillimeters(state.liftHeight_mm));
        _batteryVoltage = state.batteryVoltage;
        _accelerometer = new Vector3(state.accel.x, state.accel.y, state.accel.z);
        _gyro = new Vector3(state.gyro.x, state.gyro.y, state.gyro.z);
        _carryingObjectId = state.carryingObjectID;
        _carryingObjectOnTopId = state.carryingObjectOnTopID;
        _headTrackingObjectId = state.headTrackingObjectID;
        _localizedToObjectId = state.localizedToObjectID;
        _lastImageRobotTimestamp = state.lastImageTimeStamp;
        _robotStatusFlags = state.status;
        _gameStatusFlags = state.gameStatus;

        await DispatchEventAsync(new EvtRobotStateUpdated(this)).ConfigureAwait(false);
    }

    private void OnPerRobotSettings(PerRobotSettings settings)
    {
        _serialNumberHead = settings.serialNumberHead;
        _serialNumberBody = settings.serialNumberBody;
        _modelNumber = settings.modelNumber;
        _hwVersion = settings.hwVersion;
        _camera.SetConfig(settings.cameraConfig);
    }

    private async Task OnBehaviorTransitionAsync(BehaviorTransition transition)
    {
        if (_currentBehavior is null)
        {
            return;
        }

        if (transition.newBehaviorExecType == (byte)_currentBehavior.BehaviorType.Id)
        {
            await _currentBehavior.NotifyStartedAsync().ConfigureAwait(false);
        }
        else
        {
            await _currentBehavior.NotifyStoppedAsync().ConfigureAwait(false);
        }
    }

    private async Task OnUnexpectedMovementAsync(UnexpectedMovement movement)
    {
        CladEnum<CladMovementType>? type = CladEnum<CladMovementType>.TryFromId(
            movement.movementType, out CladEnum<CladMovementType>? foundType)
            ? foundType
            : null;
        CladEnum<CladMovementSide>? side = CladEnum<CladMovementSide>.TryFromId(
            movement.movementSide, out CladEnum<CladMovementSide>? foundSide)
            ? foundSide
            : null;

        await DispatchEventAsync(new EvtUnexpectedMovement(this, movement.timestamp, type, side))
            .ConfigureAwait(false);
    }

    /// <summary>Asks the robot to abort every in-progress action.</summary>
    public Task AbortAllActionsAsync(bool logAbortMessages = false) =>
        _actionDispatcher.AbortAllActionsAsync(logAbortMessages);

    /// <summary>Enables or disables all reaction triggers.</summary>
    public Task EnableAllReactionTriggersAsync(bool shouldEnable) => shouldEnable
        ? SendAsync(new MessageGameToEngine
        {
            RemoveDisableReactionsLock = new RemoveDisableReactionsLock { lockID = "sdk" },
        })
        : SendAsync(new MessageGameToEngine
        {
            DisableAllReactionsWithLock = new DisableAllReactionsWithLock { lockID = "sdk" },
        });

    /// <summary>Enables or disables stopping at cliffs.</summary>
    public Task EnableStopOnCliffAsync(bool enable) =>
        SendAsync(new MessageGameToEngine { EnableStopOnCliff = new EnableStopOnCliff { enable = enable } });

    /// <summary>Sets the robot's speaker volume (0 = mute, 1 = max).</summary>
    public Task SetRobotVolumeAsync(float robotVolume) =>
        SendAsync(new MessageGameToEngine
        {
            SetRobotVolume = new SetRobotVolume { robotId = (byte)RobotId, volume = robotVolume },
        });

    /// <summary>Enables or disables facial expression estimation.</summary>
    public Task EnableFacialExpressionEstimationAsync(bool enable = true) =>
        SendAsync(new MessageGameToEngine
        {
            EnableVisionMode = new EnableVisionMode
            {
                mode = (uint)global::Anki.Cozmo.VisionMode.EstimatingFacialExpression,
                enable = enable,
            },
        });

    /// <summary>Enables streaming of the connected device's IMU data.</summary>
    public Task EnableDeviceImuAsync(bool enableRaw = false, bool enableUser = false, bool enableGyro = false) =>
        SendAsync(new MessageGameToEngine
        {
            EnableDeviceIMUData = new EnableDeviceIMUData
            {
                enableAccelerometerRaw = enableRaw,
                enableAccelerometerUser = enableUser,
                enableGyro = enableGyro,
            },
        });

    /// <summary>Sets Cozmo's needs levels (0..1; clamped by the engine).</summary>
    public Task SetNeedsLevelsAsync(float repairValue = 1, float energyValue = 1, float playValue = 1) =>
        SendAsync(new MessageGameToEngine
        {
            ForceSetNeedsLevels = new ForceSetNeedsLevels
            {
                newNeedLevel = [repairValue, energyValue, playValue],
            },
        });

    /// <summary>Drives the wheels at a fixed speed until commanded otherwise.</summary>
    public Task DriveWheelMotorsAsync(
        float leftWheelSpeed,
        float rightWheelSpeed,
        float? leftWheelAccel = null,
        float? rightWheelAccel = null) =>
        SendAsync(new MessageGameToEngine
        {
            DriveWheels = new DriveWheels
            {
                lwheel_speed_mmps = leftWheelSpeed,
                rwheel_speed_mmps = rightWheelSpeed,
                lwheel_accel_mmps2 = leftWheelAccel ?? leftWheelSpeed,
                rwheel_accel_mmps2 = rightWheelAccel ?? rightWheelSpeed,
            },
        });

    /// <summary>Drives the wheels, optionally stopping after a duration.</summary>
    public async Task DriveWheelsAsync(
        float leftWheelSpeed,
        float rightWheelSpeed,
        float? leftWheelAccel = null,
        float? rightWheelAccel = null,
        TimeSpan? duration = null)
    {
        await DriveWheelMotorsAsync(leftWheelSpeed, rightWheelSpeed, leftWheelAccel, rightWheelAccel)
            .ConfigureAwait(false);
        if (duration is { } wait && wait > TimeSpan.Zero)
        {
            await Task.Delay(wait).ConfigureAwait(false);
            await StopAllMotorsAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Stops all motors.</summary>
    public Task StopAllMotorsAsync() =>
        SendAsync(new MessageGameToEngine { StopAllMotors = new StopAllMotors() });

    /// <summary>Moves the head at a speed in radians per second.</summary>
    public Task MoveHeadAsync(float speedRadiansPerSecond) =>
        SendAsync(new MessageGameToEngine
        {
            MoveHead = new MoveHead { speed_rad_per_sec = speedRadiansPerSecond },
        });

    /// <summary>Moves the lift at a speed in radians per second.</summary>
    public Task MoveLiftAsync(float speedRadiansPerSecond) =>
        SendAsync(new MessageGameToEngine
        {
            MoveLift = new MoveLift { speed_rad_per_sec = speedRadiansPerSecond },
        });

    /// <summary>Makes the robot speak text.</summary>
    public async Task<SayText> SayTextAsync(
        string text,
        bool playExcitedAnimation = false,
        bool useCozmoVoice = true,
        float durationScalar = 1.0f,
        float voicePitch = 0.0f,
        bool inParallel = false,
        byte numRetries = 0)
    {
        var action = new SayText(text, playExcitedAnimation, useCozmoVoice, durationScalar, voicePitch);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Sets the five backpack lights; null entries are left unchanged.</summary>
    public async Task SetBackpackLightsAsync(
        Light? light1,
        Light? light2,
        Light? light3,
        Light? light4,
        Light? light5)
    {
        var message = new SetBackpackLEDs();
        Light?[] lights = [light1, light2, light3, light4, light5];
        for (int index = 0; index < lights.Length; index++)
        {
            if (lights[index] is { } light)
            {
                Lights.SetLight(message, index, light);
            }
        }

        await SendAsync(new MessageGameToEngine { SetBackpackLEDs = message }).ConfigureAwait(false);
    }

    /// <summary>Sets the three center backpack lights; the outer red-only lights are turned off.</summary>
    public Task SetCenterBackpackLightsAsync(Light light) =>
        SetBackpackLightsAsync(Lights.Off, light, light, light, Lights.Off);

    /// <summary>Sets all five backpack lights to the same light.</summary>
    public Task SetAllBackpackLightsAsync(Light light) => SetBackpackLightsAsync(light, light, light, light, light);

    /// <summary>Turns all backpack lights off.</summary>
    public Task SetBackpackLightsOffAsync() =>
        SetBackpackLightsAsync(Lights.Off, Lights.Off, Lights.Off, Lights.Off, Lights.Off);

    /// <summary>Turns the IR headlight on or off.</summary>
    public Task SetHeadLightAsync(bool enable) =>
        SendAsync(new MessageGameToEngine { SetHeadlight = new SetHeadlight { enable = enable } });

    /// <summary>Enables or disables the automatic freeplay cube light mode.</summary>
    public async Task EnableFreeplayCubeLightsAsync(bool enable = true)
    {
        if (enable)
        {
            await SetCubeLightStateAsync(true).ConfigureAwait(false);
            await EnableCubeSleepAsync(false, false).ConfigureAwait(false);
        }
        else
        {
            await EnableCubeSleepAsync(true, true).ConfigureAwait(false);
            await SetCubeLightStateAsync(false).ConfigureAwait(false);
        }
    }

    /// <summary>Turns the head to an angle, clamping it to the supported range.</summary>
    public async Task<SetHeadAngle> SetHeadAngleAsync(
        Angle angle,
        float accel = 10.0f,
        float maxSpeed = 10.0f,
        float duration = 0.0f,
        bool warnOnClamp = true,
        bool inParallel = false,
        byte numRetries = 0)
    {
        var action = new SetHeadAngle(angle, maxSpeed, accel, duration, warnOnClamp);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Moves the lift to a ratio between 0 (bottom) and 1 (top).</summary>
    public async Task<SetLiftHeight> SetLiftHeightAsync(
        double height,
        float accel = 10.0f,
        float maxSpeed = 10.0f,
        float duration = 0.0f,
        bool inParallel = false,
        byte numRetries = 0)
    {
        var action = new SetLiftHeight(height, maxSpeed, accel, duration);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Sends an audio event to the engine.</summary>
    public Task PlayAudioAsync(CladEnum<Anki.AudioMetaData.GameEvent.Codelab> audioEvent)
    {
        ArgumentNullException.ThrowIfNull(audioEvent);
        return SendAsync(new MessageGameToEngine
        {
            PostAudioEvent = new Anki.AudioEngine.Multiplexer.PostAudioEvent
            {
                audioEvent = (uint)audioEvent.Id,
                gameObject = (uint)Anki.AudioMetaData.GameObjectType.CodeLab,
            },
        });
    }

    /// <summary>Plays a song and the matching custom animation.</summary>
    public async Task<Animation> PlaySongAsync(
        IReadOnlyList<SongNote> songNotes,
        uint loopCount = 1,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(songNotes);
        await SendAsync(new MessageGameToEngine
        {
            ReplaceNotesInSong = new ReplaceNotesInSong
            {
                notes = songNotes.Select(note => note.ToClad()).ToArray(),
            },
        }).ConfigureAwait(false);

        return await PlayAnimationAsync("cozmo_sings_custom", loopCount, inParallel, numRetries)
            .ConfigureAwait(false);
    }

    /// <summary>Plays a named animation.</summary>
    /// <exception cref="ArgumentException">The animation name is unknown.</exception>
    /// <exception cref="AnimationsNotLoadedException">The animation names have not loaded yet.</exception>
    public async Task<Animation> PlayAnimationAsync(
        string name,
        uint loopCount = 1,
        bool inParallel = false,
        byte numRetries = 0,
        bool ignoreBodyTrack = false,
        bool ignoreHeadTrack = false,
        bool ignoreLiftTrack = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_animNames.Contains(name))
        {
            throw new ArgumentException($"Unknown animation name \"{name}\".", nameof(name));
        }

        var action = new Animation(name, loopCount, ignoreBodyTrack, ignoreHeadTrack, ignoreLiftTrack);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Plays an animation trigger.</summary>
    public async Task<AnimationTrigger> PlayAnimationTriggerAsync(
        CladEnum<CladAnimationTrigger> trigger,
        uint loopCount = 1,
        bool inParallel = false,
        byte numRetries = 0,
        bool useLiftSafe = false,
        bool ignoreBodyTrack = false,
        bool ignoreHeadTrack = false,
        bool ignoreLiftTrack = false)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var action = new AnimationTrigger(
            trigger,
            loopCount,
            useLiftSafe,
            ignoreBodyTrack,
            ignoreHeadTrack,
            ignoreLiftTrack);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Pushes an idle animation onto the SDK's idle stack.</summary>
    public async Task SetIdleAnimationAsync(CladEnum<CladAnimationTrigger> animTrigger)
    {
        ArgumentNullException.ThrowIfNull(animTrigger);
        await SendAsync(new MessageGameToEngine
        {
            PushIdleAnimation = new PushIdleAnimation
            {
                animTrigger = (int)animTrigger.Id,
                lockName = "sdk",
            },
        }).ConfigureAwait(false);
        _idleStackDepth++;
    }

    /// <summary>Removes every idle animation the SDK pushed.</summary>
    public async Task ClearIdleAnimationAsync()
    {
        var message = new MessageGameToEngine
        {
            RemoveIdleAnimation = new RemoveIdleAnimation { lockName = "sdk" },
        };

        while (_idleStackDepth > 0)
        {
            await SendAsync(message).ConfigureAwait(false);
            _idleStackDepth--;
        }
    }

    /// <summary>Displays a packed image on the robot's OLED face.</summary>
    public async Task<DisplayOledFaceImage> DisplayOledFaceImageAsync(
        byte[] screenData,
        uint durationMs,
        bool inParallel = true)
    {
        ArgumentNullException.ThrowIfNull(screenData);

        if (_currentFaceImageAction is { IsRunning: true } running)
        {
            await running.AbortAsync().ConfigureAwait(false);
        }

        var action = new DisplayOledFaceImage(screenData, durationMs);
        _currentFaceImageAction = action;
        await _actionDispatcher.SendSingleActionAsync(action, inParallel).ConfigureAwait(false);
        return action;
    }

    /// <summary>Starts executing a behavior.</summary>
    public async Task<Behavior> StartBehaviorAsync(CladEnum<ExecutableBehaviorType> behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        if (_currentBehavior is not null)
        {
            await _currentBehavior.NotifyStoppedAsync().ConfigureAwait(false);
        }

        var behavior = new Behavior(behaviorType, async () => await SetNoneBehaviorAsync().ConfigureAwait(false));
        AddChildDispatcher(behavior);
        await behavior.RequestAsync().ConfigureAwait(false);
        await SendAsync(new MessageGameToEngine
        {
            ExecuteBehaviorByExecutableType = new ExecuteBehaviorByExecutableType
            {
                behaviorType = (byte)behaviorType.Id,
                numRuns = -1,
            },
        }).ConfigureAwait(false);
        _currentBehavior = behavior;
        return behavior;
    }

    /// <summary>Runs a behavior for at most the given time.</summary>
    public async Task RunTimedBehaviorAsync(CladEnum<ExecutableBehaviorType> behaviorType, TimeSpan activeTime)
    {
        Behavior behavior = await StartBehaviorAsync(behaviorType).ConfigureAwait(false);
        try
        {
            await behavior.WaitForCompletedAsync(activeTime).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await behavior.StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Puts the robot into freeplay mode.</summary>
    public async Task StartFreeplayBehaviorsAsync()
    {
        await SendAsync(new MessageGameToEngine
        {
            ActivateHighLevelActivity = new ActivateHighLevelActivity
            {
                activityType = (byte)HighLevelActivity.Freeplay,
            },
        }).ConfigureAwait(false);
        _isFreeplayModeActive = true;
    }

    /// <summary>Leaves freeplay mode and stops the current behavior and actions.</summary>
    public async Task StopFreeplayBehaviorsAsync()
    {
        await SendAsync(new MessageGameToEngine
        {
            ActivateHighLevelActivity = new ActivateHighLevelActivity
            {
                activityType = (byte)HighLevelActivity.Selection,
            },
        }).ConfigureAwait(false);
        _isFreeplayModeActive = false;
        await SetNoneBehaviorAsync().ConfigureAwait(false);
        await AbortAllActionsAsync().ConfigureAwait(false);
    }

    /// <summary>Picks up an object.</summary>
    /// <exception cref="NotPickupableException">The object cannot be picked up.</exception>
    public async Task<PickupObject> PickupObjectAsync(
        ObservableObject obj,
        bool usePreDockPose = true,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (!obj.Pickupable)
        {
            throw new NotPickupableException("Cannot pickup this type of object.");
        }

        var action = new PickupObject(obj, usePreDockPose);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Places the held object on another object.</summary>
    /// <exception cref="CannotPlaceObjectsOnThisException">The target cannot have objects placed on it.</exception>
    public async Task<PlaceOnObject> PlaceOnObjectAsync(
        ObservableObject obj,
        bool usePreDockPose = true,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (!obj.PlaceObjectsOnThis)
        {
            throw new CannotPlaceObjectsOnThisException("Cannot place objects on this type of object.");
        }

        var action = new PlaceOnObject(obj, usePreDockPose);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Places the held object on the ground at the robot's position.</summary>
    public async Task<PlaceObjectOnGroundHere> PlaceObjectOnGroundHereAsync(
        ObservableObject obj,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(obj);
        var action = new PlaceObjectOnGroundHere(obj);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Turns towards a face.</summary>
    public async Task<TurnTowardsFace> TurnTowardsFaceAsync(
        Face face,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(face);
        var action = new TurnTowardsFace(face);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Drives to a pose.</summary>
    public async Task<GoToPose> GoToPoseAsync(
        Pose pose,
        bool relativeToRobot = false,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Pose target = pose;
        if (relativeToRobot)
        {
            Pose robotPose = _pose ?? throw new InvalidOperationException("The robot has no pose yet.");
            target = robotPose.DefinePoseRelativeThis(pose);
        }

        var action = new GoToPose(target);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Drives to an object.</summary>
    public async Task<GoToObject> GoToObjectAsync(
        ObservableObject targetObject,
        Distance distanceFromObject,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(targetObject);
        var action = new GoToObject(
            targetObject.ObjectId ?? throw new InvalidOperationException("The object has no id."),
            distanceFromObject);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Drives to dock with a cube.</summary>
    public async Task<DockWithCube> DockWithCubeAsync(
        LightCube targetObject,
        Angle? approachAngle = null,
        CladEnum<AlignmentType>? alignmentType = null,
        Distance? distanceFromMarker = null,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(targetObject);
        var action = new DockWithCube(
            targetObject,
            approachAngle,
            alignmentType ?? RobotAlignmentTypes.LiftPlate,
            distanceFromMarker);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Rolls a cube.</summary>
    public async Task<RollCube> RollCubeAsync(
        LightCube targetObject,
        Angle? approachAngle = null,
        bool checkForObjectOnTop = false,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(targetObject);
        var action = new RollCube(targetObject, approachAngle, checkForObjectOnTop);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Pops a wheelie using a cube as leverage.</summary>
    public async Task<PopAWheelie> PopAWheelieAsync(
        LightCube targetObject,
        Angle? approachAngle = null,
        bool inParallel = false,
        byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(targetObject);
        var action = new PopAWheelie(targetObject, approachAngle);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Turns in place.</summary>
    public async Task<TurnInPlace> TurnInPlaceAsync(
        Angle angle,
        bool inParallel = false,
        byte numRetries = 0,
        Angle? speed = null,
        Angle? accel = null,
        Angle? angleTolerance = null,
        bool isAbsolute = false)
    {
        var action = new TurnInPlace(angle, speed, accel, angleTolerance, isAbsolute);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Drives off the charger contacts.</summary>
    public async Task<DriveOffChargerContacts> DriveOffChargerContactsAsync(
        bool inParallel = false,
        byte numRetries = 0)
    {
        var action = new DriveOffChargerContacts();
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Drives straight for a distance.</summary>
    public async Task<DriveStraight> DriveStraightAsync(
        Distance distance,
        Speed speed,
        bool shouldPlayAnim = true,
        bool inParallel = false,
        byte numRetries = 0)
    {
        var action = new DriveStraight(distance, speed, shouldPlayAnim);
        await _actionDispatcher.SendSingleActionAsync(action, inParallel, numRetries).ConfigureAwait(false);
        return action;
    }

    /// <summary>Reverses until the charger is detected or the time limit is reached.</summary>
    public async Task BackupOntoChargerAsync(float maxDriveTime = 3.0f)
    {
        await DriveWheelMotorsAsync(-30, -30).ConfigureAwait(false);
        DateTimeOffset deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromSeconds(maxDriveTime);
        while (!IsOnCharger && TimeProvider.System.GetUtcNow() < deadline)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }

        await StopAllMotorsAsync().ConfigureAwait(false);
    }

    /// <summary>Returns a context that takes the robot off the charger and puts it back afterwards.</summary>
    public async Task<PerformOffChargerContext> PerformOffChargerAsync()
    {
        var context = new PerformOffChargerContext(this);
        await context.EnterAsync().ConfigureAwait(false);
        return context;
    }
}
