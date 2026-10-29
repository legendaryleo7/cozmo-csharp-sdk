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

using Anki;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;

namespace Cozmo.Tests;

/// <summary>Tests the WP-09 port of <c>cozmo.faces</c>.</summary>
public sealed class FaceTests
{
    [Fact]
    public async Task ObservedFaceRaisesObservedThenAppearedAndUpdatesFields()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 7, timeProvider: clock);

        var observed = new List<EvtFaceObserved>();
        var appeared = new List<EvtFaceAppeared>();
        face.AddHandler<EvtFaceObserved>(observed.Add);
        face.AddHandler<EvtFaceAppeared>(appeared.Add);

        Assert.Equal(TimeSpan.FromSeconds(0.4), face.VisibilityTimeout);
        Assert.Equal(TimeSpan.FromSeconds(0.4), Face.FaceVisibilityTimeout);
        Assert.False(face.IsVisible);
        Assert.Equal(TimeSpan.MaxValue, face.TimeSinceLastSeen);
        Assert.Equal(string.Empty, face.Name);
        Assert.Null(face.Expression);
        Assert.Null(face.ExpressionScore);
        Assert.Null(face.KnownExpression);
        Assert.Null(face.LeftEye);
        Assert.Null(face.RightEye);
        Assert.Null(face.Nose);
        Assert.Null(face.Mouth);
        Assert.Null(face.Pose);

        await face.HandleObservedFace(ObservedMessage(
            faceId: 7,
            timestamp: 100,
            name: "Alice",
            expression: 1,
            expressionScore: 80,
            x: 10.0f));

        Assert.True(face.IsVisible);
        Assert.Single(observed);
        Assert.Single(appeared);
        Assert.Same(face, observed[0].Face);
        Assert.Same(face, appeared[0].Face);
        Assert.Equal("Alice", observed[0].Name);
        Assert.Equal("Alice", appeared[0].Name);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), observed[0].ImageBox);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), appeared[0].ImageBox);
        Assert.Equal(10.0, observed[0].Pose!.Position.X);
        Assert.Equal(10.0, appeared[0].Pose!.Position.X);
        Assert.Equal("Alice", face.Name);
        Assert.Equal(FacialExpression.Happy, face.Expression);
        Assert.Equal(80, face.ExpressionScore);
        Assert.Equal("happy", face.KnownExpression);
        Assert.Equal(100u, face.LastObservedRobotTimestamp);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), face.LastObservedImageBox);
        Assert.Equal(clock.GetUtcNow(), face.LastObservedTime);
        Assert.Equal(clock.GetUtcNow(), face.LastEventTime);

        Assert.Equal(2, face.LeftEye!.Count);
        Assert.Equal(1.0, face.LeftEye[0].X);
        Assert.Equal(2.0, face.LeftEye[0].Y);
        Assert.Equal(2.0, face.LeftEye[1].X);
        Assert.Equal(3.0, face.LeftEye[1].Y);
        Assert.Equal(3.0, face.RightEye![0].X);
        Assert.Equal(5.0, face.Nose![0].X);
        Assert.Equal(2, face.Mouth!.Count);

        var firstUpdated = new HashSet<string>(StringComparer.Ordinal)
        {
            "pose",
            "left_eye",
            "right_eye",
            "nose",
            "mouth",
            "expression",
            "expression_score",
            "last_observed_time",
            "last_observed_robot_timestamp",
            "last_event_time",
            "last_observed_image_box",
        };
        Assert.True(observed[0].Updated.SetEquals(firstUpdated));
        Assert.True(appeared[0].Updated.SetEquals(firstUpdated));

        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(100), face.TimeSinceLastSeen);

        RobotObservedFace second = ObservedMessage(
            faceId: 7,
            timestamp: 200,
            name: "Alice",
            expression: 1,
            expressionScore: 81,
            x: 11.0f);
        second.leftEye = new[] { new CladPoint2d(11.0f, 12.0f) };
        await face.HandleObservedFace(second);

        Assert.Equal(2, observed.Count);
        Assert.Single(appeared);
        Assert.Equal(11.0, observed[1].Pose!.Position.X);
        Assert.Equal(200u, face.LastObservedRobotTimestamp);
        Assert.Equal(TimeSpan.Zero, face.TimeSinceLastSeen);
        Assert.Equal(81, face.ExpressionScore);
        Assert.Equal(11.0, face.LeftEye![0].X);
        Assert.Single(face.LeftEye);

        Assert.DoesNotContain("expression", observed[1].Updated);
        Assert.Contains("expression_score", observed[1].Updated);
        Assert.True(observed[1].Updated.SetEquals(new[]
        {
            "pose",
            "left_eye",
            "right_eye",
            "nose",
            "mouth",
            "expression_score",
            "last_observed_time",
            "last_observed_robot_timestamp",
            "last_event_time",
            "last_observed_image_box",
        }));
    }

    [Fact]
    public async Task FaceVisibilityExpiresAfterTimeout()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 7, timeProvider: clock);

        var appeared = new List<EvtFaceAppeared>();
        var disappeared = new List<EvtFaceDisappeared>();
        face.AddHandler<EvtFaceAppeared>(appeared.Add);
        face.AddHandler<EvtFaceDisappeared>(disappeared.Add);

        await face.HandleObservedFace(ObservedMessage(timestamp: 10));

        clock.Advance(TimeSpan.FromMilliseconds(399));
        Assert.True(face.IsVisible);
        Assert.Empty(disappeared);

        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.False(face.IsVisible);
        Assert.Single(disappeared);
        Assert.Same(face, disappeared[0].Face);

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Single(disappeared);

        await face.HandleObservedFace(ObservedMessage(timestamp: 20));
        Assert.True(face.IsVisible);
        Assert.Equal(2, appeared.Count);
    }

    [Theory]
    [InlineData(-1, FacialExpression.Unknown)]
    [InlineData(0, FacialExpression.Neutral)]
    [InlineData(1, FacialExpression.Happy)]
    [InlineData(2, FacialExpression.Surprised)]
    [InlineData(3, FacialExpression.Angry)]
    [InlineData(4, FacialExpression.Sad)]
    [InlineData(5, FacialExpression.Unknown)]
    [InlineData(7, FacialExpression.Unknown)]
    [InlineData(127, FacialExpression.Unknown)]
    public void FacialExpressionValuesConvertToPythonStrings(int cladValue, string expected)
    {
        Assert.Equal(
            expected,
            FacialExpression.FromClad((Anki.Vision.FacialExpression)cladValue));
    }

    [Fact]
    public void FacialExpressionConstantsMatchThePythonStrings()
    {
        Assert.Equal("unknown", FacialExpression.Unknown);
        Assert.Equal("neutral", FacialExpression.Neutral);
        Assert.Equal("happy", FacialExpression.Happy);
        Assert.Equal("surprised", FacialExpression.Surprised);
        Assert.Equal("angry", FacialExpression.Angry);
        Assert.Equal("sad", FacialExpression.Sad);
    }

    [Fact]
    public async Task ZeroExpressionScoreIsReportedAsUnknown()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 3, timeProvider: clock);

        var observed = new List<EvtFaceObserved>();
        face.AddHandler<EvtFaceObserved>(observed.Add);

        await face.HandleObservedFace(ObservedMessage(
            faceId: 3,
            expression: (int)Anki.Vision.FacialExpression.Happiness,
            expressionScore: 0));

        // The engine reported a known expression, but its score was zero. Python treats that
        // as unknown because zero would otherwise overwrite the negative unknown score.
        Assert.Equal(FacialExpression.Unknown, face.Expression);
        Assert.Equal(0, face.ExpressionScore);
        Assert.Equal(string.Empty, face.KnownExpression);
        Assert.Contains("expression", observed[0].Updated);
        Assert.Contains("expression_score", observed[0].Updated);

        await face.HandleObservedFace(ObservedMessage(
            faceId: 3,
            expression: (int)Anki.Vision.FacialExpression.Happiness,
            expressionScore: 0));

        Assert.Equal(2, observed.Count);
        Assert.DoesNotContain("expression", observed[1].Updated);
        Assert.DoesNotContain("expression_score", observed[1].Updated);
    }

    [Fact]
    public async Task UnknownAndUnexpectedExpressionValuesUseUnknownAndZeroScore()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 3, timeProvider: clock);

        await face.HandleObservedFace(ObservedMessage(
            faceId: 3,
            expression: (int)Anki.Vision.FacialExpression.Unknown,
            expressionScore: 99));

        Assert.Equal(FacialExpression.Unknown, face.Expression);
        Assert.Equal(0, face.ExpressionScore);
        Assert.Equal(string.Empty, face.KnownExpression);

        await face.HandleObservedFace(ObservedMessage(
            faceId: 3,
            expression: 7,
            expressionScore: 99));

        Assert.Equal(FacialExpression.Unknown, face.Expression);
        Assert.Equal(0, face.ExpressionScore);

        await face.HandleObservedFace(ObservedMessage(
            faceId: 3,
            expression: (int)Anki.Vision.FacialExpression.Count,
            expressionScore: 99));

        Assert.Equal(FacialExpression.Unknown, face.Expression);
        Assert.Equal(0, face.ExpressionScore);
    }

    [Fact]
    public async Task FaceIdChangeRecordsUpdatedIdAndRaisesEvent()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 4, timeProvider: clock);

        var changes = new List<EvtFaceIdChanged>();
        face.AddHandler<EvtFaceIdChanged>(changes.Add);

        Assert.False(face.HasUpdatedFaceId);
        Assert.Equal(4, face.FaceId);
        Assert.Equal(4, face.UpdatedFaceId);

        await face.HandleChangedObservedFaceId(new RobotChangedObservedFaceID(oldID: 4, newID: 9));

        Assert.Single(changes);
        Assert.Same(face, changes[0].Face);
        Assert.Equal(4, changes[0].OldId);
        Assert.Equal(9, changes[0].NewId);
        Assert.True(face.HasUpdatedFaceId);
        Assert.Equal(9, face.UpdatedFaceId);
        Assert.Equal(4, face.FaceId);
    }

    [Fact]
    public async Task FaceIdCanBeReassignedWithoutThrowing()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var face = new Face(connection);

        Assert.Null(face.FaceId);
        Assert.Null(face.UpdatedFaceId);
        Assert.False(face.HasUpdatedFaceId);

        face.FaceId = 1;
        Assert.Equal(1, face.FaceId);
        Assert.Equal(1, face.UpdatedFaceId);

        // Python raises ValueError on reassignment (faces.py:248-253); the port logs a
        // warning instead so the world can repair its lookup table.
        face.FaceId = 2;
        Assert.Equal(2, face.FaceId);
        Assert.Equal(2, face.UpdatedFaceId);

        face.FaceId = null;
        Assert.Null(face.FaceId);
    }

    [Fact]
    public async Task RenamedAndErasedEnrollmentUpdateNameAndRaiseEvents()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 5, timeProvider: clock);

        var renamed = new List<EvtFaceRenamed>();
        var erased = new List<EvtErasedEnrolledFace>();
        face.AddHandler<EvtFaceRenamed>(renamed.Add);
        face.AddHandler<EvtErasedEnrolledFace>(erased.Add);

        await face.HandleObservedFace(ObservedMessage(faceId: 5, name: "Alice"));

        await face.HandleRenamedEnrolledFace(
            new Anki.Vision.RobotRenamedEnrolledFace(faceID: 5, name: "Bob"));

        Assert.Equal("Bob", face.Name);
        Assert.Single(renamed);
        Assert.Same(face, renamed[0].Face);
        Assert.Equal("Alice", renamed[0].OldName);
        Assert.Equal("Bob", renamed[0].NewName);

        await face.HandleErasedEnrolledFace(
            new RobotErasedEnrolledFace(faceID: 5, name: "ignored"));

        Assert.Equal(string.Empty, face.Name);
        Assert.Single(erased);
        Assert.Same(face, erased[0].Face);
        Assert.Equal("Bob", erased[0].OldName);
    }

    [Fact]
    public async Task NameFaceSendsSetFaceToEnrollMessage()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var face = new Face(connection, faceId: 42);

        await face.NameFaceAsync("Alice");

        MessageGameToEngine sent = DecodeGameToEngine(Assert.Single(transport.SentFrames));
        Assert.Equal(MessageGameToEngine.Tag.SetFaceToEnroll, sent.tag);
        SetFaceToEnroll message = sent.SetFaceToEnroll;
        Assert.Equal("Alice", message.name);
        Assert.Equal(42, message.observedID);
        Assert.Equal(0, message.saveID);
        Assert.True(message.saveToRobot);
        Assert.False(message.sayName);
        Assert.False(message.useMusic);
    }

    [Fact]
    public async Task RenameFaceSendsUpdateEnrolledFaceMessage()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var face = new Face(connection, faceId: 42, timeProvider: clock);

        await face.HandleObservedFace(ObservedMessage(faceId: 42, name: "Alice"));
        await face.RenameFaceAsync("Bob");

        MessageGameToEngine sent = DecodeGameToEngine(Assert.Single(transport.SentFrames));
        Assert.Equal(MessageGameToEngine.Tag.UpdateEnrolledFaceByID, sent.tag);
        UpdateEnrolledFaceByID message = sent.UpdateEnrolledFaceByID;
        Assert.Equal(42, message.faceID);
        Assert.Equal("Alice", message.oldName);
        Assert.Equal("Bob", message.newName);
    }

    [Fact]
    public async Task EraseEnrolledFaceSendsEraseByIdMessage()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var face = new Face(connection, faceId: 42);

        await face.EraseEnrolledFaceAsync();

        MessageGameToEngine sent = DecodeGameToEngine(Assert.Single(transport.SentFrames));
        Assert.Equal(MessageGameToEngine.Tag.EraseEnrolledFaceByID, sent.tag);
        Assert.Equal(42, sent.EraseEnrolledFaceByID.faceID);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Alice2")]
    [InlineData("Alice Smith")]
    [InlineData("Bøb")]
    [InlineData("Alice_")]
    public async Task InvalidNamesAreRejectedWithoutSending(string name)
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var face = new Face(connection, faceId: 42);

        Assert.Throws<ArgumentException>(() => { _ = face.NameFaceAsync(name); });
        Assert.Throws<ArgumentException>(() => { _ = face.RenameFaceAsync(name); });
        Assert.Empty(transport.SentFrames);
    }

    [Fact]
    public async Task CommandsRequireAFaceId()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var face = new Face(connection);

        Assert.Throws<InvalidOperationException>(() => { _ = face.NameFaceAsync("Alice"); });
        Assert.Throws<InvalidOperationException>(() => { _ = face.RenameFaceAsync("Bob"); });
        Assert.Throws<InvalidOperationException>(() => { _ = face.EraseEnrolledFaceAsync(); });
        Assert.Empty(transport.SentFrames);
    }

    [Fact]
    public async Task ModuleHelpersSendEnrollmentMessages()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);

        await Faces.EraseAllEnrolledFacesAsync(connection);
        MessageGameToEngine eraseAll = DecodeGameToEngine(transport.SentFrames[0]);
        Assert.Equal(MessageGameToEngine.Tag.EraseAllEnrolledFaces, eraseAll.tag);
        Assert.NotNull(eraseAll.EraseAllEnrolledFaces);

        await Faces.EraseEnrolledFaceByIdAsync(connection, 12);
        MessageGameToEngine eraseById = DecodeGameToEngine(transport.SentFrames[1]);
        Assert.Equal(MessageGameToEngine.Tag.EraseEnrolledFaceByID, eraseById.tag);
        Assert.Equal(12, eraseById.EraseEnrolledFaceByID.faceID);

        await Faces.UpdateEnrolledFaceByIdAsync(connection, 12, "Alice", "Bob");
        MessageGameToEngine update = DecodeGameToEngine(transport.SentFrames[2]);
        Assert.Equal(MessageGameToEngine.Tag.UpdateEnrolledFaceByID, update.tag);
        Assert.Equal(12, update.UpdateEnrolledFaceByID.faceID);
        Assert.Equal("Alice", update.UpdateEnrolledFaceByID.oldName);
        Assert.Equal("Bob", update.UpdateEnrolledFaceByID.newName);

        Assert.Throws<ArgumentNullException>(() => { _ = Faces.EraseAllEnrolledFacesAsync(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = Faces.EraseEnrolledFaceByIdAsync(null!, 1); });
        Assert.Throws<ArgumentNullException>(() => { _ = Faces.UpdateEnrolledFaceByIdAsync(connection, 1, null!, "Bob"); });
        Assert.Throws<ArgumentNullException>(() => { _ = Faces.UpdateEnrolledFaceByIdAsync(connection, 1, "Alice", null!); });
    }

    private static RobotObservedFace ObservedMessage(
        int faceId = 7,
        uint timestamp = 100,
        string name = "Alice",
        int expression = 0,
        int expressionScore = 50,
        float x = 1.0f)
    {
        var expressionValues = new byte[5];
        if (expression >= 0 && expression < expressionValues.Length)
        {
            expressionValues[expression] = (byte)expressionScore;
        }

        return new RobotObservedFace
        {
            faceID = faceId,
            timestamp = timestamp,
            name = name,
            expression = (sbyte)expression,
            expressionValues = expressionValues,
            pose = new PoseStruct3d(x, 2.0f, 3.0f, 1.0f, 0.0f, 0.0f, 0.0f, 5),
            img_rect = new CladRect(1.0f, 2.0f, 3.0f, 4.0f),
            leftEye = new[] { new CladPoint2d(1.0f, 2.0f), new CladPoint2d(2.0f, 3.0f) },
            rightEye = new[] { new CladPoint2d(3.0f, 4.0f) },
            nose = new[] { new CladPoint2d(5.0f, 6.0f) },
            mouth = new[] { new CladPoint2d(7.0f, 8.0f), new CladPoint2d(9.0f, 10.0f) },
        };
    }

    private static MessageGameToEngine DecodeGameToEngine(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
    }
}
