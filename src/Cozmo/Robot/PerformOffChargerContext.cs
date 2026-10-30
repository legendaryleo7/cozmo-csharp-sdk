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

/// <summary>Drives the robot off the charger, then back onto it when disposed.</summary>
/// <remarks>
/// Port of <c>robot.py::PerformOffChargerContext</c>. Python uses a context manager; C# uses
/// <see cref="IAsyncDisposable"/>: <c>await using var context = await robot.PerformOffChargerAsync();</c>
/// </remarks>
public sealed class PerformOffChargerContext : IAsyncDisposable
{
    private readonly Robot _robot;
    private bool _wasOnCharger;

    internal PerformOffChargerContext(Robot robot)
    {
        _robot = robot;
    }

    /// <summary>True when the robot was on the charger when the context was entered.</summary>
    public bool WasOnCharger => _wasOnCharger;

    /// <summary>Moves the robot off the charger contacts when it was on the charger.</summary>
    internal async Task EnterAsync()
    {
        _wasOnCharger = _robot.IsOnCharger;
        if (_wasOnCharger)
        {
            DriveOffChargerContacts action = await _robot.DriveOffChargerContactsAsync(inParallel: true)
                .ConfigureAwait(false);
            await action.WaitForCompletedAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Reverses the robot back onto the charger when it started on the charger.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_wasOnCharger)
        {
            await _robot.BackupOntoChargerAsync().ConfigureAwait(false);
        }
    }
}
