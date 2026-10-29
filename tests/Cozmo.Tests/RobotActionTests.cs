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

using Anki.Cozmo.Sdk;
using CladCozmo = Anki.Cozmo;
using CladIface = Anki.Cozmo.ExternalInterface;
using Xunit;

namespace Cozmo.Tests;

public sealed class RobotActionTests
{
    private const double Tolerance = 1e-5;

    [Fact]
    public void LiftPositionConvertsBetweenHeightRatioAndAngle()
    {
        LiftPosition position = LiftPosition.FromRatio(0.5);

        Assert.Equal(0.5, position.Ratio, Tolerance);
        Assert.Equal(62.0, position.Height.DistanceMillimeters, Tolerance);
        Assert.Equal(0.26051246336734035, position.Angle.Radians, Tolerance);
        Assert.Equal(
            position.Height.DistanceMillimeters,
            LiftPosition.FromAngle(position.Angle).Height.DistanceMillimeters,
            Tolerance);
        Assert.Equal(-0.1982661014310845, LiftPosition.MinLiftAngle.Radians, Tolerance);
        Assert.Equal(0.7925150244593042, LiftPosition.MaxLiftAngle.Radians, Tolerance);
    }

    [Fact]
    public void LiftPositionRejectsUnreachableAngle()
    {
        LiftPosition position = LiftPosition.FromHeight(Distance.FromMillimeters(200));

        Assert.Throws<InvalidOperationException>(() => position.Angle);
    }

    [Fact]
    public void GoToPoseEncodesPositionAndAngle()
    {
        Pose pose = Pose.FromZAngle(10, 20, 30, Util.Degrees(45));

        CladIface.RobotActionUnion union = Encode(new GoToPose(pose));

        Assert.Equal(CladIface.RobotActionUnion.Tag.gotoPose, union.tag);
        Assert.Equal(10f, union.gotoPose.x_mm);
        Assert.Equal(20f, union.gotoPose.y_mm);
        Assert.Equal((float)(Math.PI / 4), union.gotoPose.rad);
    }

    [Fact]
    public void GoToObjectEncodesDistance()
    {
        CladIface.RobotActionUnion union = Encode(new GoToObject(5, Distance.FromMillimeters(100)));

        Assert.Equal(CladIface.RobotActionUnion.Tag.gotoObject, union.tag);
        Assert.Equal(5, union.gotoObject.objectID);
        Assert.Equal(100f, union.gotoObject.distanceFromObjectOrigin_mm);
        Assert.False(union.gotoObject.usePreDockPose);
        Assert.False(union.gotoObject.useManualSpeed);
    }

    [Fact]
    public void DockWithCubeUsesApproachAngleForPreDockPose()
    {
        LightCube cube = NewCube();
        CladEnum<CladCozmo.AlignmentType> alignment = CladEnum<CladCozmo.AlignmentType>.FromName("LIFT_PLATE");

        CladIface.RobotActionUnion plain = Encode(new DockWithCube(cube, null, alignment));
        Assert.False(plain.alignWithObject.useApproachAngle);
        Assert.False(plain.alignWithObject.usePreDockPose);
        Assert.Equal(0f, plain.alignWithObject.approachAngle_rad);
        Assert.Equal((byte)alignment.Id, plain.alignWithObject.alignmentType);
        Assert.Equal(cube.ObjectId!.Value, plain.alignWithObject.objectID);

        CladIface.RobotActionUnion angled = Encode(new DockWithCube(cube, Util.Degrees(30), alignment));
        Assert.True(angled.alignWithObject.useApproachAngle);
        Assert.True(angled.alignWithObject.usePreDockPose);
        Assert.Equal((float)(Math.PI / 6), angled.alignWithObject.approachAngle_rad);
    }

    [Fact]
    public void RollCubeEncodesCheckForObjectOnTop()
    {
        CladIface.RobotActionUnion union = Encode(new RollCube(NewCube(), null, checkForObjectOnTop: false));

        Assert.Equal(CladIface.RobotActionUnion.Tag.rollObject, union.tag);
        Assert.False(union.rollObject.checkForObjectOnTop);
        Assert.False(union.rollObject.useApproachAngle);
    }

    [Fact]
    public void DriveStraightEncodesDistanceSpeedAndAnimationFlag()
    {
        CladIface.RobotActionUnion union = Encode(new DriveStraight(
            Distance.FromMillimeters(200),
            Speed.FromMillimetersPerSecond(50),
            shouldPlayAnim: false));

        Assert.Equal(CladIface.RobotActionUnion.Tag.driveStraight, union.tag);
        Assert.Equal(200f, union.driveStraight.dist_mm);
        Assert.Equal(50f, union.driveStraight.speed_mmps);
        Assert.False(union.driveStraight.shouldPlayAnimation);
    }

    [Fact]
    public void DisplayOledFaceImageEncodesPackedPixels()
    {
        byte[] screen = [0x01, 0x02, 0xFF];
        CladIface.RobotActionUnion union = Encode(new DisplayOledFaceImage(screen, 1000));

        Assert.Equal(CladIface.RobotActionUnion.Tag.displayFaceImage, union.tag);
        Assert.Equal(screen, union.displayFaceImage.faceData);
        Assert.Equal(1000u, union.displayFaceImage.duration_ms);
    }

    [Fact]
    public void ObjectActionsEncodeObjectId()
    {
        LightCube cube = NewCube();

        CladIface.RobotActionUnion pickup = Encode(new PickupObject(cube, usePreDockPose: true));
        Assert.Equal(CladIface.RobotActionUnion.Tag.pickupObject, pickup.tag);
        Assert.Equal(cube.ObjectId, pickup.pickupObject.objectID);
        Assert.True(pickup.pickupObject.usePreDockPose);

        CladIface.RobotActionUnion place = Encode(new PlaceOnObject(cube, usePreDockPose: false));
        Assert.Equal(CladIface.RobotActionUnion.Tag.placeOnObject, place.tag);
        Assert.Equal(cube.ObjectId, place.placeOnObject.objectID);
        Assert.False(place.placeOnObject.usePreDockPose);

        CladIface.RobotActionUnion ground = Encode(new PlaceObjectOnGroundHere(cube));
        Assert.Equal(CladIface.RobotActionUnion.Tag.placeObjectOnGroundHere, ground.tag);
    }

    [Theory]
    [InlineData(true, true, CladCozmo.AnimationTrigger.OnSawNewNamedFace, CladCozmo.SayTextVoiceStyle.CozmoProcessing_Sentence)]
    [InlineData(false, false, CladCozmo.AnimationTrigger.Count, CladCozmo.SayTextVoiceStyle.Unprocessed)]
    public void SayTextSelectsAnimationAndVoiceStyle(
        bool excited,
        bool cozmoVoice,
        CladCozmo.AnimationTrigger expectedEvent,
        CladCozmo.SayTextVoiceStyle expectedStyle)
    {
        CladIface.RobotActionUnion union = Encode(new SayText("hello", excited, cozmoVoice, 1.5f, 1.25f));

        Assert.Equal(CladIface.RobotActionUnion.Tag.sayText, union.tag);
        Assert.Equal("hello", union.sayText.text);
        Assert.Equal((int)expectedEvent, union.sayText.playEvent);
        Assert.Equal((byte)expectedStyle, union.sayText.voiceStyle);
        Assert.Equal(1.5f, union.sayText.durationScalar);
        Assert.Equal(1.25f, union.sayText.voicePitch);
        Assert.False(union.sayText.fitToDuration);
    }

    [Theory]
    [InlineData(-30.0, -25.0)]
    [InlineData(50.0, 44.5)]
    [InlineData(10.0, 10.0)]
    public void HeadAngleIsClamped(double requested, double expected)
    {
        var action = new SetHeadAngle(Util.Degrees(requested), 1, 2, 3, warnOnClamp: false);
        CladIface.RobotActionUnion union = Encode(action);

        Assert.Equal((float)Util.Degrees(expected).Radians, union.setHeadAngle.angle_rad, 1e-5);
        Assert.Equal(1f, union.setHeadAngle.max_speed_rad_per_sec);
        Assert.Equal(2f, union.setHeadAngle.accel_rad_per_sec2);
        Assert.Equal(3f, union.setHeadAngle.duration_sec);
    }

    [Theory]
    [InlineData(-0.5, 32.0)]
    [InlineData(1.5, 92.0)]
    [InlineData(0.5, 62.0)]
    public void LiftHeightRatioIsClamped(double ratio, double expectedMillimeters)
    {
        CladIface.RobotActionUnion union = Encode(new SetLiftHeight(ratio, 1, 2, 3));

        Assert.Equal((float)expectedMillimeters, union.setLiftHeight.height_mm, 1e-5);
    }

    [Fact]
    public void TurnInPlaceDefaultsOptionalAnglesToZero()
    {
        CladIface.RobotActionUnion union = Encode(
            new TurnInPlace(Util.Degrees(90), null, null, null, isAbsolute: true));

        Assert.Equal(CladIface.RobotActionUnion.Tag.turnInPlace, union.tag);
        Assert.Equal((float)(Math.PI / 2), union.turnInPlace.angle_rad, 1e-5);
        Assert.Equal(0f, union.turnInPlace.speed_rad_per_sec);
        Assert.Equal(0f, union.turnInPlace.accel_rad_per_sec2);
        Assert.Equal(0f, union.turnInPlace.tol_rad);
        Assert.Equal(1, union.turnInPlace.isAbsolute);
    }

    [Fact]
    public void PopAWheelieEncodesApproachAngle()
    {
        CladIface.RobotActionUnion union = Encode(new PopAWheelie(NewCube(), Util.Degrees(15)));

        Assert.Equal(CladIface.RobotActionUnion.Tag.popAWheelie, union.tag);
        Assert.True(union.popAWheelie.useApproachAngle);
        Assert.True(union.popAWheelie.usePreDockPose);
        Assert.False(union.popAWheelie.useManualSpeed);
        Assert.Equal((float)(Math.PI / 12), union.popAWheelie.approachAngle_rad, 1e-5);
    }

    [Fact]
    public void RobotAlignmentAndMovementWrappersMatchCladIds()
    {
        Assert.Equal((long)CladCozmo.AlignmentType.LIFT_FINGER, RobotAlignmentTypes.LiftFinger.Id);
        Assert.Equal((long)CladCozmo.AlignmentType.LIFT_PLATE, RobotAlignmentTypes.LiftPlate.Id);
        Assert.Equal((long)CladCozmo.AlignmentType.BODY, RobotAlignmentTypes.Body.Id);
        Assert.Equal((long)CladCozmo.AlignmentType.CUSTOM, RobotAlignmentTypes.Custom.Id);

        Assert.Equal((long)CladCozmo.UnexpectedMovementSide.UNKNOWN, UnexpectedMovementSide.Unknown.Id);
        Assert.Equal((long)CladCozmo.UnexpectedMovementSide.FRONT, UnexpectedMovementSide.Front.Id);
        Assert.Equal((long)CladCozmo.UnexpectedMovementSide.BACK, UnexpectedMovementSide.Back.Id);
        Assert.Equal((long)CladCozmo.UnexpectedMovementSide.LEFT, UnexpectedMovementSide.Left.Id);
        Assert.Equal((long)CladCozmo.UnexpectedMovementSide.RIGHT, UnexpectedMovementSide.Right.Id);

        Assert.Equal((long)CladCozmo.UnexpectedMovementType.TURNED_BUT_STOPPED,
            UnexpectedMovementType.TurnedButStopped.Id);
        Assert.Equal((long)CladCozmo.UnexpectedMovementType.TURNED_IN_SAME_DIRECTION,
            UnexpectedMovementType.TurnedInSameDirection.Id);
        Assert.Equal((long)CladCozmo.UnexpectedMovementType.TURNED_IN_OPPOSITE_DIRECTION,
            UnexpectedMovementType.TurnedInOppositeDirection.Id);
    }

    [Fact]
    public void DriveOffChargerContactsEncodesUnionTag()
    {
        CladIface.RobotActionUnion union = Encode(new DriveOffChargerContacts());

        Assert.Equal(CladIface.RobotActionUnion.Tag.driveOffChargerContacts, union.tag);
    }

    [Fact]
    public void ObjectActionsRejectObjectsWithoutIds()
    {
        LightCube cube = NewCube(objectId: null);

        Assert.Throws<InvalidOperationException>(() => Encode(new PickupObject(cube, usePreDockPose: true)));
    }

    private static CladIface.RobotActionUnion Encode(CozmoAction action)
    {
        var union = new CladIface.RobotActionUnion();
        action.Encode(union);
        return union;
    }

    private static LightCube NewCube(int? objectId = 7)
    {
        var connection = new CozmoConnection(new FakeTransport());
        return new LightCube(CustomObjectTypes.LightCube1Id, connection, objectId: objectId);
    }
}
