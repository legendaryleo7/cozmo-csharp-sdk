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

/// <summary>Raised when the robot has been initialized and is ready for commands.</summary>
public sealed record EvtRobotReady(Robot Robot) : CozmoEvent;

/// <summary>Raised whenever the robot's state is updated (several times per second).</summary>
public sealed record EvtRobotStateUpdated(Robot Robot) : CozmoEvent;

/// <summary>Raised when the robot does not move as expected.</summary>
/// <param name="Robot">The robot reporting the movement.</param>
/// <param name="Timestamp">Robot timestamp of the movement.</param>
/// <param name="MovementType">Kind of unexpected movement, or null when the engine reports an unknown id.</param>
/// <param name="MovementSide">Side of the obstruction, or null when the engine reports an unknown id.</param>
public sealed record EvtUnexpectedMovement(
    Robot Robot,
    uint Timestamp,
    CladEnum<global::Anki.Cozmo.UnexpectedMovementType>? MovementType,
    CladEnum<global::Anki.Cozmo.UnexpectedMovementSide>? MovementSide) : CozmoEvent;
