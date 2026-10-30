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

/// <summary>The robot surface the <see cref="World"/> needs.</summary>
/// <remarks>
/// <para>
/// The real <c>Robot</c> (WP-11) implements this interface and also adds the
/// <see cref="World"/> as a child dispatcher, so the world receives the robot's action and
/// behavior events without an explicit subscription.
/// </para>
/// <para>
/// Python's <c>World.__init__</c> takes the robot object directly (world.py:109-120); the
/// interface keeps the world testable without a robot instance.
/// </para>
/// </remarks>
public interface IWorldRobot
{
    /// <summary>The active connection to the engine.</summary>
    CozmoConnection Connection { get; }

    /// <summary>The engine's id for the robot.</summary>
    int RobotId { get; }

    /// <summary>The robot's latest pose, or null before the first state update.</summary>
    Pose? Pose { get; }

    /// <summary>The robot's camera, used for raw image events.</summary>
    Camera Camera { get; }
}
