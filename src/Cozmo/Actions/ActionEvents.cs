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

/// <summary>Lifecycle state of a <see cref="CozmoAction"/>.</summary>
public enum ActionState
{
    /// <summary>The action has not been sent to the robot.</summary>
    Idle,

    /// <summary>The action is in progress.</summary>
    Running,

    /// <summary>The action was asked to abort and is awaiting its completion message.</summary>
    Aborting,

    /// <summary>The action completed successfully.</summary>
    Succeeded,

    /// <summary>The action completed with a failure.</summary>
    Failed,
}

/// <summary>Raised when a robot starts an action.</summary>
public sealed record EvtActionStarted(CozmoAction Action) : CozmoEvent;

/// <summary>Raised when a robot action completes or fails.</summary>
/// <remarks>Concrete actions may dispatch a subclass carrying action-specific result data.</remarks>
public record EvtActionCompleted(
    CozmoAction Action,
    ActionState State,
    string? FailureCode,
    string? FailureReason) : CozmoEvent;
