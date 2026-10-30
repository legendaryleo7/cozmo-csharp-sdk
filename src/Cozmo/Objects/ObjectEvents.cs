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

/// <summary>Raised whenever an object is visually identified by the robot.</summary>
/// <param name="Obj">The object that was observed.</param>
/// <param name="Updated">
/// Names of the object fields that were updated. The observation event always includes <c>pose</c>,
/// <c>last_observed_time</c>, <c>last_observed_robot_timestamp</c>,
/// <c>last_event_time</c> and <c>last_observed_image_box</c>.
/// </param>
/// <param name="ImageBox">Where the object was last visible within Cozmo's camera view.</param>
/// <param name="Pose">The pose of the object at the time of the observation.</param>
/// <remarks>
/// A stream of these events is produced while an object is visible to the robot. Register
/// for <see cref="EvtObjectAppeared"/> instead when only the first event of a visible period
/// matters.
/// </remarks>
public sealed record EvtObjectObserved(
    ObservableObject Obj,
    IReadOnlySet<string> Updated,
    ImageBox ImageBox,
    Pose? Pose) : CozmoEvent;

/// <summary>Raised when an object first becomes visible to the robot.</summary>
/// <param name="Obj">The object that appeared.</param>
/// <param name="Updated">
/// The names of the object fields that were updated, using the same set supplied to the
/// observation that made the object visible.
/// </param>
/// <param name="ImageBox">Where the object was first visible within Cozmo's camera view.</param>
/// <param name="Pose">The pose of the object at the time it appeared.</param>
/// <remarks>
/// If the object disappears for longer than <see cref="ObservableElement.VisibilityTimeout"/>
/// seconds and is then observed again, <see cref="EvtObjectDisappeared"/> is raised first,
/// followed by another <see cref="EvtObjectAppeared"/>.
/// </remarks>
public sealed record EvtObjectAppeared(
    ObservableObject Obj,
    IReadOnlySet<string> Updated,
    ImageBox ImageBox,
    Pose? Pose) : CozmoEvent;

/// <summary>Raised when the engine reports that an object is connected.</summary>
/// <param name="Obj">The object that is connected.</param>
/// <param name="Connected">
/// <see langword="true"/> because the connected-object-states message confirms the object exists.
/// </param>
public sealed record EvtObjectConnected(ObservableObject Obj, bool Connected) : CozmoEvent;

/// <summary>Raised when an active object connects to or disconnects from the robot.</summary>
/// <param name="Obj">The object that connected or disconnected.</param>
/// <param name="Connected"><see langword="true"/> when the object connected.</param>
public sealed record EvtObjectConnectChanged(ObservableObject Obj, bool Connected) : CozmoEvent;

/// <summary>Raised when the engine reports that an object is located (its pose is known).</summary>
/// <param name="Obj">The object that is located.</param>
/// <param name="Updated">
/// The names of the object fields that were updated. An <see cref="EvtObjectLocated"/> event
/// always reports <c>pose</c> and <c>last_observed_robot_timestamp</c>.
/// </param>
/// <param name="Pose">The pose of the located object.</param>
public sealed record EvtObjectLocated(
    ObservableObject Obj,
    IReadOnlySet<string> Updated,
    Pose? Pose) : CozmoEvent;

/// <summary>Raised whenever an object that was previously being observed is no longer visible.</summary>
/// <param name="Obj">The object that is no longer being observed.</param>
public sealed record EvtObjectDisappeared(ObservableObject Obj) : CozmoEvent;

/// <summary>Raised while an active object is moving.</summary>
/// <param name="Obj">The object that is moving.</param>
/// <param name="Acceleration">The currently measured acceleration.</param>
/// <param name="MoveDuration">
/// The current duration of time the object has spent moving, in seconds, measured from the
/// most recent movement start.
/// </param>
public sealed record EvtObjectMoving(
    ObservableObject Obj,
    Vector3 Acceleration,
    double MoveDuration) : CozmoEvent;

/// <summary>Raised when an active object starts moving.</summary>
/// <param name="Obj">The object that started moving.</param>
/// <param name="Acceleration">The currently measured acceleration.</param>
public sealed record EvtObjectMovingStarted(
    ObservableObject Obj,
    Vector3 Acceleration) : CozmoEvent;

/// <summary>Raised when an active object stops moving.</summary>
/// <param name="Obj">The object that stopped moving.</param>
/// <param name="MoveDuration">
/// The duration of time the object spent moving, in seconds. It is zero for movements short
/// enough that the engine reports a stop without a preceding move.
/// </param>
public sealed record EvtObjectMovingStopped(
    ObservableObject Obj,
    double MoveDuration) : CozmoEvent;

/// <summary>Raised when an active object is tapped.</summary>
/// <param name="Obj">The object that was tapped.</param>
/// <param name="TapCount">Number of taps detected.</param>
/// <param name="TapDuration">Duration of the tap in milliseconds.</param>
/// <param name="TapIntensity">Intensity of the tap, computed as <c>tapPos - tapNeg</c>.</param>
public sealed record EvtObjectTapped(
    ObservableObject Obj,
    byte TapCount,
    byte TapDuration,
    int TapIntensity) : CozmoEvent;
