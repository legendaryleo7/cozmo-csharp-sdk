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

using Microsoft.Extensions.Logging;

namespace Anki.Cozmo.Sdk;

/// <summary>Cozmo's charger, which the robot can observe and drive toward.</summary>
/// <remarks>
/// Port of <c>Charger</c> in <c>cozmo/objects.py</c> (objects.py:638-649). The class adds no
/// state of its own to <see cref="ObservableObject"/>; it exists so callers and the world can
/// distinguish the charger from cubes and custom objects.
/// </remarks>
public sealed class Charger : ObservableObject
{
    /// <summary>Creates a charger object.</summary>
    /// <param name="connection">The connection used to send commands for this charger.</param>
    /// <param name="world">The world that owns this charger; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="objectId">The engine object id, or <see langword="null"/> before allocation.</param>
    /// <param name="timeProvider">Clock and timer source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this charger.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public Charger(
        CozmoConnection connection,
        IWorldContext? world = null,
        int? objectId = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(connection, world, objectId, timeProvider, dispatchParent, logger)
    {
    }
}
