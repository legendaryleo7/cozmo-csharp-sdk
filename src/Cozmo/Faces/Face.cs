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
using Microsoft.Extensions.Logging;

namespace Anki.Cozmo.Sdk;

/// <summary>A single face that Cozmo has detected.</summary>
/// <remarks>
/// <para>
/// Port of <c>Face</c> in <c>cozmo/faces.py</c>. A face may represent a previously enrolled
/// person, in which case <see cref="Name"/> holds the enrolled name. Each instance has a
/// <see cref="FaceId"/> that the engine may later supersede if it gets a better view and
/// changes its prediction about which face it is looking at.
/// </para>
/// <para>
/// Message subscriptions intentionally live in the world layer (WP-11), matching the Python
/// design: <c>cozmo/world.py</c> receives each CLAD face message, looks up the face by id, and
/// forwards the state to it. See <see cref="HandleObservedFace"/>,
/// <see cref="HandleChangedObservedFaceId"/>, <see cref="HandleRenamedEnrolledFace"/> and
/// <see cref="HandleErasedEnrolledFace"/>.
/// </para>
/// </remarks>
public sealed class Face : ObservableElement
{
    /// <summary>
    /// Length of time to go without receiving an observation before the face is considered
    /// invisible.
    /// </summary>
    /// <remarks>Python <c>FACE_VISIBILITY_TIMEOUT</c> (faces.py:57), which aliases the object visibility timeout.</remarks>
    public static readonly TimeSpan FaceVisibilityTimeout = ObservableElement.ObjectVisibilityTimeout;

    private int? _faceId;
    private int? _updatedFaceId;
    private string _name = string.Empty;
    private string? _expression;
    private int? _expressionScore;
    private IReadOnlyList<Vector2>? _leftEye;
    private IReadOnlyList<Vector2>? _rightEye;
    private IReadOnlyList<Vector2>? _nose;
    private IReadOnlyList<Vector2>? _mouth;

    /// <summary>Creates a face.</summary>
    /// <param name="connection">The connection used to send commands for this face.</param>
    /// <param name="world">The world that owns this face; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="faceId">The engine face id, or <see langword="null"/> before allocation.</param>
    /// <param name="timeProvider">Clock and timer source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this face.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public Face(
        CozmoConnection connection,
        IWorldContext? world = null,
        int? faceId = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(connection, world, timeProvider, dispatchParent, logger)
    {
        _faceId = faceId;
    }

    /// <summary>The internal id assigned to the face.</summary>
    /// <remarks>
    /// <para>
    /// The value is <see langword="null"/> until the world allocates the face. Python's setter
    /// raises <c>ValueError</c> when the id is assigned twice (faces.py:248-253). The port
    /// instead logs a warning and applies the new value, matching how
    /// <see cref="ObservableObject.ObjectId"/> handles an identity change (objects.py:382-393):
    /// the engine can supersede a tracked face, and the world (WP-11) must be able to repair
    /// its lookup table rather than crash.
    /// </para>
    /// </remarks>
    public int? FaceId
    {
        get => _faceId;
        set
        {
            if (_faceId is not null)
            {
                Logger.LogWarning(
                    "Changing face_id for {FaceType} from {OldId} to {NewId}.",
                    GetType().Name,
                    _faceId.Value,
                    value);
            }
            else
            {
                Logger.LogDebug("Setting face_id for {FaceType} to {NewId}.", GetType().Name, value);
            }

            _faceId = value;
        }
    }

    /// <summary><see langword="true"/> if this face has been updated or superseded by a face with a new id.</summary>
    /// <remarks>Python <c>has_updated_face_id</c> (faces.py:255-259).</remarks>
    public bool HasUpdatedFaceId => _updatedFaceId is not null;

    /// <summary>The id for the face that superseded this one, or <see cref="FaceId"/> when it has not been superseded.</summary>
    /// <remarks>Python <c>updated_face_id</c> (faces.py:260-266).</remarks>
    public int? UpdatedFaceId => _updatedFaceId ?? _faceId;

    /// <summary>The name Cozmo has associated with the face in his memory.</summary>
    /// <remarks>
    /// The string is empty when the face is not recognized or enrolled. It is updated by
    /// observations and by the renamed and erased-enrollment messages.
    /// </remarks>
    public string Name => _name;

    /// <summary>The facial expression Cozmo has recognized on the face.</summary>
    /// <remarks>
    /// <see langword="null"/> until the face has been observed, otherwise one of the
    /// <see cref="FacialExpression"/> constants. It is
    /// <see cref="FacialExpression.Unknown"/> by default unless
    /// <c>Robot.EnableFacialExpressionEstimationAsync</c> was called.
    /// </remarks>
    public string? Expression => _expression;

    /// <summary>The score/confidence that <see cref="Expression"/> was correct.</summary>
    /// <remarks>
    /// <see langword="null"/> until the face has been observed. It is <c>0</c> when the
    /// expression is <see cref="FacialExpression.Unknown"/>, for example because expression
    /// estimation has not been enabled. The maximum possible score is <c>100</c>.
    /// </remarks>
    public int? ExpressionScore => _expressionScore;

    /// <summary>The known facial expression Cozmo has recognized on the face.</summary>
    /// <remarks>
    /// Like <see cref="Expression"/> but returns an empty string for the unknown expression.
    /// Mirrors Python <c>known_expression</c> (faces.py:299-308), including its behaviour for a
    /// face that has not been observed yet (<see langword="null"/>).
    /// </remarks>
    public string? KnownExpression =>
        string.Equals(_expression, FacialExpression.Unknown, StringComparison.Ordinal)
            ? string.Empty
            : _expression;

    /// <summary>The points that outline the left eye, or <see langword="null"/> before the first observation.</summary>
    /// <remarks>
    /// Python exposes <c>left_eye</c> as a sequence of <c>(x, y)</c> tuples (faces.py:310-313);
    /// the port converts the CLAD points to <see cref="Vector2"/> values.
    /// </remarks>
    public IReadOnlyList<Vector2>? LeftEye => _leftEye;

    /// <summary>The points that outline the right eye, or <see langword="null"/> before the first observation.</summary>
    /// <remarks>Python <c>right_eye</c> (faces.py:315-318).</remarks>
    public IReadOnlyList<Vector2>? RightEye => _rightEye;

    /// <summary>The points that outline the nose, or <see langword="null"/> before the first observation.</summary>
    /// <remarks>Python <c>nose</c> (faces.py:320-323).</remarks>
    public IReadOnlyList<Vector2>? Nose => _nose;

    /// <summary>The points that outline the mouth, or <see langword="null"/> before the first observation.</summary>
    /// <remarks>Python <c>mouth</c> (faces.py:325-328).</remarks>
    public IReadOnlyList<Vector2>? Mouth => _mouth;

    /// <inheritdoc />
    /// <remarks>Uses <see cref="FaceVisibilityTimeout"/> (faces.py:207).</remarks>
    public override TimeSpan VisibilityTimeout => FaceVisibilityTimeout;

    /// <summary>Applies a robot-observed-face message.</summary>
    /// <param name="message">The CLAD observation message routed by the world.</param>
    /// <returns>A task that completes after the observed and, on first sight, appeared events are dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Port of <c>_recv_msg_robot_observed_face</c> (faces.py:332-363).</para>
    /// <para>
    /// The expression score is copied from <c>expressionValues</c> at the index of the reported
    /// expression. A known expression with a score of zero is turned into
    /// <see cref="FacialExpression.Unknown"/>: the engine marks a low-confidence result as an
    /// explicit zero, and a zero would otherwise overwrite the default negative score that the
    /// SDK uses for unknown (the comment calls this an engine bug).
    /// </para>
    /// </remarks>
    public async Task HandleObservedFace(RobotObservedFace message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var changedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "pose",
            "left_eye",
            "right_eye",
            "nose",
            "mouth",
        };

        Pose = Anki.Cozmo.Sdk.Pose.FromClad(message.pose);
        _name = message.name;

        string expression = FacialExpression.FromClad((Anki.Vision.FacialExpression)message.expression);
        int expressionScore = 0;

        if (!string.Equals(expression, FacialExpression.Unknown, StringComparison.Ordinal))
        {
            int expressionIndex = message.expression;
            if (expressionIndex >= 0 && expressionIndex < message.expressionValues.Length)
            {
                expressionScore = message.expressionValues[expressionIndex];
                if (expressionScore == 0)
                {
                    // The expression should have been marked unknown - this is a bug in the
                    // engine because even a zero score overwrites the default negative score
                    // for Unknown (faces.py:343-347).
                    expression = FacialExpression.Unknown;
                }
            }
            else
            {
                // Python indexes expressionValues with the raw value and would raise
                // IndexError. A malformed message must not abort dispatch, so treat it as
                // unknown instead.
                expression = FacialExpression.Unknown;
            }
        }

        if (!string.Equals(expression, _expression, StringComparison.Ordinal))
        {
            _expression = expression;
            changedFields.Add("expression");
        }

        if (_expressionScore != expressionScore)
        {
            _expressionScore = expressionScore;
            changedFields.Add("expression_score");
        }

        _leftEye = ConvertLandmarks(message.leftEye);
        _rightEye = ConvertLandmarks(message.rightEye);
        _nose = ConvertLandmarks(message.nose);
        _mouth = ConvertLandmarks(message.mouth);

        ImageBox imageBox = ImageBox.FromCladRect(message.img_rect);
        await OnObservedAsync(imageBox, message.timestamp, changedFields).ConfigureAwait(false);
    }

    /// <summary>Applies a robot-changed-observed-face-id message.</summary>
    /// <param name="message">The CLAD id-change message routed by the world.</param>
    /// <returns>A task that completes after the id-changed event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Port of <c>_recv_msg_robot_changed_observed_face_id</c> (faces.py:365-367). The face's
    /// <see cref="FaceId"/> is not changed; <see cref="UpdatedFaceId"/> records the replacement.
    /// </remarks>
    public async Task HandleChangedObservedFaceId(RobotChangedObservedFaceID message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _updatedFaceId = message.newID;
        await DispatchEventAsync(new EvtFaceIdChanged(this, message.oldID, message.newID)).ConfigureAwait(false);
    }

    /// <summary>Applies a robot-renamed-enrolled-face message.</summary>
    /// <param name="message">The CLAD rename message routed by the world.</param>
    /// <returns>A task that completes after the renamed event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of <c>_recv_msg_robot_renamed_enrolled_face</c> (faces.py:369-372).</remarks>
    public async Task HandleRenamedEnrolledFace(Anki.Vision.RobotRenamedEnrolledFace message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string oldName = _name;
        _name = message.name;
        await DispatchEventAsync(new EvtFaceRenamed(this, oldName, message.name)).ConfigureAwait(false);
    }

    /// <summary>Applies a robot-erased-enrolled-face message.</summary>
    /// <param name="message">The CLAD erase message routed by the world.</param>
    /// <returns>A task that completes after the erased-enrollment event is dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Port of <c>_recv_msg_robot_erased_enrolled_face</c> (faces.py:374-377). The message's
    /// <c>name</c> field is not used; the event reports the name this face held before the
    /// erase.
    /// </remarks>
    public async Task HandleErasedEnrolledFace(RobotErasedEnrolledFace message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string oldName = _name;
        _name = string.Empty;
        await DispatchEventAsync(new EvtErasedEnrolledFace(this, oldName)).ConfigureAwait(false);
    }

    /// <summary>Assigns a name to this face; Cozmo remembers it between SDK runs.</summary>
    /// <param name="name">
    /// The name to assign. It must be a non-empty string of ASCII alphabetic characters only.
    /// </param>
    /// <returns>A task that completes after the enroll message is sent.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid face name.</exception>
    /// <exception cref="InvalidOperationException">The face has no id yet.</exception>
    /// <remarks>
    /// Port of Python <c>Face.name_face</c> (faces.py:395-422). Python also starts the
    /// <c>_EnrollFace</c> behavior and returns it; starting behaviors requires the robot object
    /// model (WP-11), so this WP sends the CLAD enrollment message only. The behavior start is
    /// left for the world/robot layer to add when it wires faces to a robot.
    /// </remarks>
    public Task NameFaceAsync(string name)
    {
        ValidateName(name);
        int faceId = RequireFaceId();

        var message = new SetFaceToEnroll
        {
            name = name,
            observedID = faceId,
            saveID = 0,
            saveToRobot = true,
            sayName = false,
            useMusic = false,
        };

        return Connection.SendAsync(new MessageGameToEngine { SetFaceToEnroll = message });
    }

    /// <summary>Changes the name assigned to the face; Cozmo remembers it between SDK runs.</summary>
    /// <param name="newName">
    /// The new name. It must be a non-empty string of ASCII alphabetic characters only.
    /// </param>
    /// <returns>A task that completes after the update message is sent.</returns>
    /// <exception cref="ArgumentException"><paramref name="newName"/> is not a valid face name.</exception>
    /// <exception cref="InvalidOperationException">The face has no id yet.</exception>
    /// <remarks>Port of Python <c>Face.rename_face</c> (faces.py:424-436).</remarks>
    public Task RenameFaceAsync(string newName)
    {
        ValidateName(newName);
        return Faces.UpdateEnrolledFaceByIdAsync(Connection, RequireFaceId(), Name, newName);
    }

    /// <summary>Removes the name associated with this face.</summary>
    /// <returns>A task that completes after the erase message is sent.</returns>
    /// <exception cref="InvalidOperationException">The face has no id yet.</exception>
    /// <remarks>
    /// Port of Python <c>Face.erase_enrolled_face</c> (faces.py:438-443). Cozmo no longer
    /// remembers the associated name between SDK runs.
    /// </remarks>
    public Task EraseEnrolledFaceAsync() =>
        Faces.EraseEnrolledFaceByIdAsync(Connection, RequireFaceId());

    /// <inheritdoc />
    protected override Task DispatchObservedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) =>
        DispatchEventAsync(new EvtFaceObserved(this, _name, changedFields, imageBox, Pose));

    /// <inheritdoc />
    protected override Task DispatchAppearedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) =>
        DispatchEventAsync(new EvtFaceAppeared(this, _name, changedFields, imageBox, Pose));

    /// <inheritdoc />
    protected override Task DispatchDisappearedEventAsync() =>
        DispatchEventAsync(new EvtFaceDisappeared(this));

    private static IReadOnlyList<Vector2> ConvertLandmarks(CladPoint2d[] points)
    {
        var result = new Vector2[points.Length];
        for (int index = 0; index < points.Length; index++)
        {
            result[index] = new Vector2(points[index].x, points[index].y);
        }

        return result;
    }

    private static void ValidateName(string name)
    {
        // Python checks a truthy string whose isalpha() is true and that encodes as ASCII
        // (faces.py:385-393); the combination is exactly ASCII letters.
        bool valid = !string.IsNullOrEmpty(name) &&
            name.All(static character =>
                character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'));

        if (!valid)
        {
            throw new ArgumentException(
                $"Face name '{name}' is invalid. It must be non-empty and contain only alphabetic ASCII characters.",
                nameof(name));
        }
    }

    private int RequireFaceId() => _faceId ?? throw new InvalidOperationException(
        "Cannot send a face enrollment command before the engine has assigned a face id.");
}

/// <summary>Connection-level face-enrollment helpers.</summary>
/// <remarks>
/// Port of the module-level functions at the bottom of <c>cozmo/faces.py</c>
/// (faces.py:140-171). They send CLAD messages without needing a <see cref="Face"/> instance.
/// </remarks>
public static class Faces
{
    /// <summary>Erases the enrollment (name) records for all faces.</summary>
    /// <param name="connection">The connection to send the message over.</param>
    /// <returns>A task that completes after the message is sent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of Python <c>erase_all_enrolled_faces</c> (faces.py:140-148).</remarks>
    public static Task EraseAllEnrolledFacesAsync(CozmoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.SendAsync(
            new MessageGameToEngine { EraseAllEnrolledFaces = new EraseAllEnrolledFaces() });
    }

    /// <summary>Erases the enrollment (name) record for the face with this id.</summary>
    /// <param name="connection">The connection to send the message over.</param>
    /// <param name="faceId">The id of the face to erase.</param>
    /// <returns>A task that completes after the message is sent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <remarks>Port of Python <c>erase_enrolled_face_by_id</c> (faces.py:150-158).</remarks>
    public static Task EraseEnrolledFaceByIdAsync(CozmoConnection connection, int faceId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.SendAsync(
            new MessageGameToEngine
            {
                EraseEnrolledFaceByID = new EraseEnrolledFaceByID { faceID = faceId },
            });
    }

    /// <summary>Updates the name enrolled for a given face.</summary>
    /// <param name="connection">The connection to send the message over.</param>
    /// <param name="faceId">The id of the face to rename.</param>
    /// <param name="oldName">The old name; it must be correct or the engine ignores the message.</param>
    /// <param name="newName">The new name for the face.</param>
    /// <returns>A task that completes after the message is sent.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/>, <paramref name="oldName"/> or <paramref name="newName"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <remarks>Port of Python <c>update_enrolled_face_by_id</c> (faces.py:161-171).</remarks>
    public static Task UpdateEnrolledFaceByIdAsync(
        CozmoConnection connection,
        int faceId,
        string oldName,
        string newName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);

        return connection.SendAsync(
            new MessageGameToEngine
            {
                UpdateEnrolledFaceByID = new UpdateEnrolledFaceByID
                {
                    faceID = faceId,
                    oldName = oldName,
                    newName = newName,
                },
            });
    }
}
