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

/// <summary>Raised when animation names have been received from the engine.</summary>
public sealed record EvtAnimationsLoaded : CozmoEvent;

/// <summary>Raised when an animation or animation trigger completes.</summary>
/// <remarks>
/// The action-specific <see cref="AnimationName"/> is carried in addition to the four
/// <see cref="EvtActionCompleted"/> fields.
/// </remarks>
/// <param name="Action">The action that completed.</param>
/// <param name="State">Final action state.</param>
/// <param name="FailureCode">Short failure code, or null when the action succeeded.</param>
/// <param name="FailureReason">Human-readable failure reason, or null when the action succeeded.</param>
/// <param name="AnimationName">Name of the animation or trigger that completed.</param>
public sealed record EvtAnimationCompleted(
    CozmoAction Action,
    ActionState State,
    string? FailureCode,
    string? FailureReason,
    string AnimationName) : EvtActionCompleted(Action, State, FailureCode, FailureReason);
