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

namespace Anki.Cozmo.Sdk;

/// <summary>Raised whenever a face is visually identified by the robot.</summary>
/// <param name="Face">The face that was observed.</param>
/// <param name="Name">The name associated with the face at the time of the observation.</param>
/// <param name="Updated">
/// The names of the face fields that were updated. Mirrors the Python event's <c>updated</c>
/// set; every observation includes <c>pose</c>, <c>left_eye</c>, <c>right_eye</c>, <c>nose</c>,
/// <c>mouth</c>, <c>last_observed_time</c>, <c>last_observed_robot_timestamp</c>,
/// <c>last_event_time</c> and <c>last_observed_image_box</c>, plus <c>expression</c> and
/// <c>expression_score</c> when those changed.
/// </param>
/// <param name="ImageBox">Where the face was last visible within Cozmo's camera view.</param>
/// <param name="Pose">The pose of the face at the time of the observation.</param>
/// <remarks>
/// A stream of these events is produced while a face is visible to the robot. Register for
/// <see cref="EvtFaceAppeared"/> instead when only the first event of a visible period matters.
/// </remarks>
public sealed record EvtFaceObserved(
    Face Face,
    string Name,
    IReadOnlySet<string> Updated,
    ImageBox ImageBox,
    Pose? Pose) : CozmoEvent;

/// <summary>Raised when a face first becomes visible to the robot.</summary>
/// <param name="Face">The face that appeared.</param>
/// <param name="Name">The name associated with the face when it appeared.</param>
/// <param name="Updated">
/// The names of the face fields that were updated, using the same set supplied to the
/// observation that made the face visible.
/// </param>
/// <param name="ImageBox">Where the face was first visible within Cozmo's camera view.</param>
/// <param name="Pose">The pose of the face at the time it appeared.</param>
/// <remarks>
/// If the face disappears for longer than <see cref="Face.FaceVisibilityTimeout"/> and is then
/// observed again, <see cref="EvtFaceDisappeared"/> is raised first, followed by another
/// <see cref="EvtFaceAppeared"/>.
/// </remarks>
public sealed record EvtFaceAppeared(
    Face Face,
    string Name,
    IReadOnlySet<string> Updated,
    ImageBox ImageBox,
    Pose? Pose) : CozmoEvent;

/// <summary>Raised whenever a face that was previously being observed is no longer visible.</summary>
/// <param name="Face">The face that is no longer being observed.</param>
public sealed record EvtFaceDisappeared(Face Face) : CozmoEvent;

/// <summary>Raised whenever a face has its id updated by the engine.</summary>
/// <param name="Face">The face that is being given a new id.</param>
/// <param name="OldId">The id previously used for this face.</param>
/// <param name="NewId">The new id that will be used for this face.</param>
/// <remarks>
/// Generally raised when a tracked but unrecognized face (negative id) is recognized and
/// receives a positive id, or when face records are merged after the engine realizes that two
/// tracked faces are actually the same face. The face's <see cref="Face.FaceId"/> keeps its old
/// value; <see cref="Face.UpdatedFaceId"/> reports the new id.
/// </remarks>
public sealed record EvtFaceIdChanged(Face Face, int OldId, int NewId) : CozmoEvent;

/// <summary>Raised whenever a face is renamed by the engine.</summary>
/// <param name="Face">The face that is being given a new name.</param>
/// <param name="OldName">The name previously used for this face.</param>
/// <param name="NewName">The new name that will be used for this face.</param>
public sealed record EvtFaceRenamed(Face Face, string OldName, string NewName) : CozmoEvent;

/// <summary>Raised when a face enrollment is removed.</summary>
/// <param name="Face">The face whose enrollment is being erased.</param>
/// <param name="OldName">The name previously used for this face.</param>
public sealed record EvtErasedEnrolledFace(Face Face, string OldName) : CozmoEvent;
