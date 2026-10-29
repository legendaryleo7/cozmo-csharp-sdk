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

using Anki.Cozmo.ExternalInterface;
using Microsoft.Extensions.Logging;

namespace Anki.Cozmo.Sdk;

/// <summary>Base for the simple robot actions defined by robot.py.</summary>
/// <remarks>
/// Each subclass stores the documented Python arguments and fills one
/// <see cref="RobotActionUnion"/> alternative. The action state machine, abort handling
/// and completion events live in <see cref="CozmoAction"/>.
/// </remarks>
public abstract class RobotAction : CozmoAction
{
    /// <summary>Creates a robot action.</summary>
    /// <param name="enableAbortLogging">False to silence abort messages for frequently aborted actions.</param>
    protected RobotAction(bool enableAbortLogging = true)
        : base(enableAbortLogging)
    {
    }

    /// <summary>Returns the object id, raising when the object has never been assigned one.</summary>
    protected static int RequireObjectId(ObservableObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return obj.ObjectId ?? throw new InvalidOperationException("The object has no object id.");
    }
}

/// <summary>Drives the robot to a pose in the world.</summary>
public sealed class GoToPose : RobotAction
{
    /// <summary>Creates the action.</summary>
    public GoToPose(Pose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Pose = pose;
    }

    /// <summary>The destination pose.</summary>
    public Pose Pose { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.gotoPose = new GotoPose
    {
        x_mm = (float)Pose.Position.X,
        y_mm = (float)Pose.Position.Y,
        rad = (float)Pose.Rotation.AngleZ.Radians,
    };
}

/// <summary>Drives the robot to an object.</summary>
public sealed class GoToObject : RobotAction
{
    /// <summary>Creates the action.</summary>
    public GoToObject(int objectId, Distance distanceFromObject)
    {
        ObjectId = objectId;
        DistanceFromObject = distanceFromObject;
    }

    /// <summary>Id of the target object.</summary>
    public int ObjectId { get; }

    /// <summary>Distance from the object's origin to stop at.</summary>
    public Distance DistanceFromObject { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.gotoObject = new GotoObject
    {
        objectID = ObjectId,
        distanceFromObjectOrigin_mm = (float)DistanceFromObject.DistanceMillimeters,
        useManualSpeed = false,
        usePreDockPose = false,
    };
}

/// <summary>Drives the robot to dock with an object.</summary>
public sealed class DockWithCube : RobotAction
{
    /// <summary>Creates the action.</summary>
    public DockWithCube(
        ObservableObject obj,
        Angle? approachAngle,
        CladEnum<global::Anki.Cozmo.AlignmentType> alignmentType,
        Distance? distanceFromMarker = null)
    {
        ArgumentNullException.ThrowIfNull(alignmentType);
        Obj = obj ?? throw new ArgumentNullException(nameof(obj));
        UseApproachAngle = approachAngle is not null;
        ApproachAngle = approachAngle ?? Util.Degrees(0);
        AlignmentType = alignmentType;
        DistanceFromMarker = distanceFromMarker ?? Distance.FromMillimeters(0);
    }

    /// <summary>The object to dock with.</summary>
    public ObservableObject Obj { get; }

    /// <summary>True when an explicit approach angle was supplied.</summary>
    public bool UseApproachAngle { get; }

    /// <summary>Approach angle, or zero when none was supplied.</summary>
    public Angle ApproachAngle { get; }

    /// <summary>Alignment type passed to the engine.</summary>
    public CladEnum<global::Anki.Cozmo.AlignmentType> AlignmentType { get; }

    /// <summary>Distance from the marker, or zero when none was supplied.</summary>
    public Distance DistanceFromMarker { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.alignWithObject = new AlignWithObject
    {
        objectID = RequireObjectId(Obj),
        distanceFromMarker_mm = (float)DistanceFromMarker.DistanceMillimeters,
        approachAngle_rad = (float)ApproachAngle.Radians,
        alignmentType = (byte)AlignmentType.Id,
        useApproachAngle = UseApproachAngle,
        usePreDockPose = UseApproachAngle, // robot.py:242 uses the approach-angle flag for both.
        useManualSpeed = false,
    };
}

/// <summary>Rolls an object.</summary>
public sealed class RollCube : RobotAction
{
    /// <summary>Creates the action.</summary>
    public RollCube(ObservableObject obj, Angle? approachAngle, bool checkForObjectOnTop)
    {
        Obj = obj ?? throw new ArgumentNullException(nameof(obj));
        UseApproachAngle = approachAngle is not null;
        ApproachAngle = approachAngle ?? Util.Degrees(0);
        CheckForObjectOnTop = checkForObjectOnTop;
    }

    /// <summary>The object to roll.</summary>
    public ObservableObject Obj { get; }

    /// <summary>True when an explicit approach angle was supplied.</summary>
    public bool UseApproachAngle { get; }

    /// <summary>Approach angle, or zero when none was supplied.</summary>
    public Angle ApproachAngle { get; }

    /// <summary>True to ask the engine to check for an object on top.</summary>
    public bool CheckForObjectOnTop { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.rollObject = new RollObject
    {
        objectID = RequireObjectId(Obj),
        approachAngle_rad = (float)ApproachAngle.Radians,
        useApproachAngle = UseApproachAngle,
        usePreDockPose = UseApproachAngle,
        useManualSpeed = false,
        checkForObjectOnTop = CheckForObjectOnTop,
    };
}

/// <summary>Drives the robot off the charger contacts.</summary>
public sealed class DriveOffChargerContacts : RobotAction
{
    /// <summary>Creates the action.</summary>
    public DriveOffChargerContacts()
    {
    }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) =>
        action.driveOffChargerContacts = new global::Anki.Cozmo.ExternalInterface.DriveOffChargerContacts();
}

/// <summary>Drives the robot straight for a distance.</summary>
public sealed class DriveStraight : RobotAction
{
    /// <summary>Creates the action.</summary>
    public DriveStraight(Distance distance, Speed speed, bool shouldPlayAnim)
    {
        Distance = distance;
        Speed = speed;
        ShouldPlayAnim = shouldPlayAnim;
    }

    /// <summary>Distance to drive.</summary>
    public Distance Distance { get; }

    /// <summary>Speed to drive at.</summary>
    public Speed Speed { get; }

    /// <summary>True to play an animation while driving.</summary>
    public bool ShouldPlayAnim { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.driveStraight = new global::Anki.Cozmo.ExternalInterface.DriveStraight
    {
        speed_mmps = (float)Speed.SpeedMmps,
        dist_mm = (float)Distance.DistanceMillimeters,
        shouldPlayAnimation = ShouldPlayAnim,
    };
}

/// <summary>Displays a packed image on the robot's OLED face.</summary>
public sealed class DisplayOledFaceImage : RobotAction
{
    /// <summary>Creates the action.</summary>
    /// <param name="screenData">Packed pixel data, eight pixels per byte.</param>
    /// <param name="durationMs">How long to display the image.</param>
    public DisplayOledFaceImage(byte[] screenData, uint durationMs)
        : base(enableAbortLogging: false) // robot.py:317: faces are sent frequently and aborted often.
    {
        ScreenData = screenData ?? throw new ArgumentNullException(nameof(screenData));
        DurationMs = durationMs;
    }

    /// <summary>Packed pixel data, eight pixels per byte.</summary>
    public byte[] ScreenData { get; }

    /// <summary>How long to display the image, in milliseconds.</summary>
    public uint DurationMs { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.displayFaceImage = new DisplayFaceImage
    {
        faceData = ScreenData,
        duration_ms = DurationMs,
    };
}

/// <summary>Picks up an object.</summary>
public sealed class PickupObject : RobotAction
{
    /// <summary>Creates the action.</summary>
    public PickupObject(ObservableObject obj, bool usePreDockPose)
    {
        Obj = obj ?? throw new ArgumentNullException(nameof(obj));
        UsePreDockPose = usePreDockPose;
    }

    /// <summary>The object to pick up.</summary>
    public ObservableObject Obj { get; }

    /// <summary>True to drive to a pre-dock pose first.</summary>
    public bool UsePreDockPose { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.pickupObject = new global::Anki.Cozmo.ExternalInterface.PickupObject
    {
        objectID = RequireObjectId(Obj),
        usePreDockPose = UsePreDockPose,
    };
}

/// <summary>Places the held object on another object.</summary>
public sealed class PlaceOnObject : RobotAction
{
    /// <summary>Creates the action.</summary>
    public PlaceOnObject(ObservableObject obj, bool usePreDockPose)
    {
        Obj = obj ?? throw new ArgumentNullException(nameof(obj));
        UsePreDockPose = usePreDockPose;
    }

    /// <summary>The object to place onto.</summary>
    public ObservableObject Obj { get; }

    /// <summary>True to drive to a pre-dock pose first.</summary>
    public bool UsePreDockPose { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.placeOnObject = new global::Anki.Cozmo.ExternalInterface.PlaceOnObject
    {
        objectID = RequireObjectId(Obj),
        usePreDockPose = UsePreDockPose,
    };
}

/// <summary>Places the held object on the ground at the robot's current position.</summary>
public sealed class PlaceObjectOnGroundHere : RobotAction
{
    /// <summary>Creates the action.</summary>
    public PlaceObjectOnGroundHere(ObservableObject obj)
    {
        Obj = obj ?? throw new ArgumentNullException(nameof(obj));
    }

    /// <summary>The held object.</summary>
    public ObservableObject Obj { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) =>
        action.placeObjectOnGroundHere = new global::Anki.Cozmo.ExternalInterface.PlaceObjectOnGroundHere();
}

/// <summary>Makes the robot speak text.</summary>
public sealed class SayText : RobotAction
{
    /// <summary>Creates the action.</summary>
    public SayText(
        string text,
        bool playExcitedAnimation,
        bool useCozmoVoice,
        float durationScalar,
        float voicePitch)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        PlayEvent = playExcitedAnimation
            ? global::Anki.Cozmo.AnimationTrigger.OnSawNewNamedFace
            : global::Anki.Cozmo.AnimationTrigger.Count; // robot.py:428 - until SdkTextToSpeech works.
        SayStyle = useCozmoVoice
            ? global::Anki.Cozmo.SayTextVoiceStyle.CozmoProcessing_Sentence
            : global::Anki.Cozmo.SayTextVoiceStyle.Unprocessed;
        DurationScalar = durationScalar;
        VoicePitch = voicePitch;
    }

    /// <summary>Text to speak.</summary>
    public string Text { get; }

    /// <summary>Animation trigger played while speaking.</summary>
    public global::Anki.Cozmo.AnimationTrigger PlayEvent { get; }

    /// <summary>Voice style used by the engine.</summary>
    public global::Anki.Cozmo.SayTextVoiceStyle SayStyle { get; }

    /// <summary>Duration scalar passed to the engine.</summary>
    public float DurationScalar { get; }

    /// <summary>Voice pitch passed to the engine.</summary>
    public float VoicePitch { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.sayText = new global::Anki.Cozmo.ExternalInterface.SayText
    {
        text = Text,
        playEvent = (int)PlayEvent,
        voiceStyle = (byte)SayStyle,
        durationScalar = DurationScalar,
        voicePitch = VoicePitch,
        fitToDuration = false,
    };
}

/// <summary>Sets the robot's head angle.</summary>
public sealed class SetHeadAngle : RobotAction
{
    /// <summary>Lowest supported head angle (robot.py:99).</summary>
    public static readonly Angle MinHeadAngle = Angle.FromDegrees(-25);

    /// <summary>Highest supported head angle (robot.py:102).</summary>
    public static readonly Angle MaxHeadAngle = Angle.FromDegrees(44.5);

    /// <summary>Creates the action, clamping the angle to the supported range.</summary>
    public SetHeadAngle(Angle angle, float maxSpeed, float accel, float duration, bool warnOnClamp = true)
    {
        if (angle < MinHeadAngle)
        {
            if (warnOnClamp)
            {
                Logger.LogWarning("Clamping head angle from {Angle} to min {Min}.", angle, MinHeadAngle);
            }

            Angle = MinHeadAngle;
        }
        else if (angle > MaxHeadAngle)
        {
            if (warnOnClamp)
            {
                Logger.LogWarning("Clamping head angle from {Angle} to max {Max}.", angle, MaxHeadAngle);
            }

            Angle = MaxHeadAngle;
        }
        else
        {
            Angle = angle;
        }

        MaxSpeed = maxSpeed;
        Accel = accel;
        Duration = duration;
    }

    /// <summary>Clamped head angle.</summary>
    public Angle Angle { get; }

    /// <summary>Maximum speed in radians per second.</summary>
    public float MaxSpeed { get; }

    /// <summary>Acceleration in radians per second squared.</summary>
    public float Accel { get; }

    /// <summary>Time for the head to turn, in seconds.</summary>
    public float Duration { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.setHeadAngle = new global::Anki.Cozmo.ExternalInterface.SetHeadAngle
    {
        angle_rad = (float)Angle.Radians,
        max_speed_rad_per_sec = MaxSpeed,
        accel_rad_per_sec2 = Accel,
        duration_sec = Duration,
    };
}

/// <summary>Sets the robot's lift height.</summary>
public sealed class SetLiftHeight : RobotAction
{
    /// <summary>Creates the action, clamping the ratio to 0..1.</summary>
    public SetLiftHeight(double height, float maxSpeed, float accel, float duration)
    {
        if (height < 0.0)
        {
            Logger.LogWarning("Lift height {Height} is too small for the 0..1 range; clamping.", height);
            LiftHeightMillimeters = LiftPosition.MinLiftHeightMillimeters;
        }
        else if (height > 1.0)
        {
            Logger.LogWarning("Lift height {Height} is too large for the 0..1 range; clamping.", height);
            LiftHeightMillimeters = LiftPosition.MaxLiftHeightMillimeters;
        }
        else
        {
            LiftHeightMillimeters = LiftPosition.MinLiftHeightMillimeters +
                (height * (LiftPosition.MaxLiftHeightMillimeters - LiftPosition.MinLiftHeightMillimeters));
        }

        MaxSpeed = maxSpeed;
        Accel = accel;
        Duration = duration;
    }

    /// <summary>Lift height in millimetres after clamping.</summary>
    public double LiftHeightMillimeters { get; }

    /// <summary>Maximum speed in radians per second.</summary>
    public float MaxSpeed { get; }

    /// <summary>Acceleration in radians per second squared.</summary>
    public float Accel { get; }

    /// <summary>Time for the lift to move, in seconds.</summary>
    public float Duration { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.setLiftHeight = new global::Anki.Cozmo.ExternalInterface.SetLiftHeight
    {
        height_mm = (float)LiftHeightMillimeters,
        max_speed_rad_per_sec = MaxSpeed,
        accel_rad_per_sec2 = Accel,
        duration_sec = Duration,
    };
}

/// <summary>Turns the robot in place.</summary>
public sealed class TurnInPlace : RobotAction
{
    /// <summary>Creates the action.</summary>
    public TurnInPlace(Angle angle, Angle? speed, Angle? accel, Angle? angleTolerance, bool isAbsolute)
    {
        Angle = angle;
        Speed = speed;
        Accel = accel;
        AngleTolerance = angleTolerance;
        IsAbsolute = isAbsolute;
    }

    /// <summary>Angle to turn.</summary>
    public Angle Angle { get; }

    /// <summary>Angular speed, or null to use the engine default.</summary>
    public Angle? Speed { get; }

    /// <summary>Angular acceleration, or null to use the engine default.</summary>
    public Angle? Accel { get; }

    /// <summary>Angular tolerance, or null to use the engine default.</summary>
    public Angle? AngleTolerance { get; }

    /// <summary>True to turn to an absolute angle, false to turn relative to the current pose.</summary>
    public bool IsAbsolute { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.turnInPlace = new global::Anki.Cozmo.ExternalInterface.TurnInPlace
    {
        angle_rad = (float)Angle.Radians,
        speed_rad_per_sec = RadiansOrZero(Speed),
        accel_rad_per_sec2 = RadiansOrZero(Accel),
        tol_rad = RadiansOrZero(AngleTolerance),
        isAbsolute = (byte)(IsAbsolute ? 1 : 0),
    };

    private static float RadiansOrZero(Angle? angle) => angle is null ? 0.0f : (float)angle.Value.Radians;
}

/// <summary>Turns the robot towards a face.</summary>
public sealed class TurnTowardsFace : RobotAction
{
    /// <summary>Creates the action.</summary>
    /// <param name="face">The face to turn towards.</param>
    /// <exception cref="InvalidOperationException">The face has no id yet.</exception>
    public TurnTowardsFace(Face face)
    {
        Face = face ?? throw new ArgumentNullException(nameof(face));
        FaceId = face.FaceId ?? throw new InvalidOperationException("The face has no face id.");
    }

    /// <summary>The face to turn towards.</summary>
    public Face Face { get; }

    /// <summary>Id of the face to turn towards.</summary>
    public int FaceId { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.turnTowardsFace = new global::Anki.Cozmo.ExternalInterface.TurnTowardsFace
    {
        faceID = FaceId,
        maxTurnAngle_rad = (float)Math.PI, // robot.py:626 - 180 degrees.
    };
}

/// <summary>Pops a wheelie using an object as leverage.</summary>
public sealed class PopAWheelie : RobotAction
{
    /// <summary>Creates the action.</summary>
    public PopAWheelie(ObservableObject obj, Angle? approachAngle)
    {
        Obj = obj ?? throw new ArgumentNullException(nameof(obj));
        UseApproachAngle = approachAngle is not null;
        ApproachAngle = approachAngle ?? Util.Degrees(0);
    }

    /// <summary>The object used as leverage.</summary>
    public ObservableObject Obj { get; }

    /// <summary>True when an explicit approach angle was supplied.</summary>
    public bool UseApproachAngle { get; }

    /// <summary>Approach angle, or zero when none was supplied.</summary>
    public Angle ApproachAngle { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action) => action.popAWheelie = new global::Anki.Cozmo.ExternalInterface.PopAWheelie
    {
        objectID = RequireObjectId(Obj),
        approachAngle_rad = (float)ApproachAngle.Radians,
        useApproachAngle = UseApproachAngle,
        usePreDockPose = UseApproachAngle, // robot.py:604 uses the approach-angle flag for both.
        useManualSpeed = false,
    };
}
