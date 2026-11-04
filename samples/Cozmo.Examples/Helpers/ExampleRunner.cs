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

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples;

/// <summary>Connects, optionally drives off the charger, runs a program, and disconnects.</summary>
/// <remarks>
/// The Python SDK drives off the charger contacts before running a program unless
/// <c>drive_off_charger_on_connect</c> is false. The C# SDK leaves that decision to the
/// caller, so the ported examples use this helper to match the Python behavior.
/// </remarks>
public static class ExampleRunner
{
    /// <summary>Runs a program against a freshly connected robot.</summary>
    /// <param name="program">The example body.</param>
    /// <param name="driveOffChargerOnConnect">
    /// True to send <c>DriveOffChargerContacts</c> before the program, matching the Python
    /// default. Pass false for examples that set <c>drive_off_charger_on_connect = False</c>.
    /// </param>
    /// <param name="connection">Optional connector; null uses the first available device.</param>
    /// <param name="cancellationToken">Cancels connection setup.</param>
    public static async Task RunAsync(
        Func<Robot, Task> program,
        bool driveOffChargerOnConnect = true,
        IDeviceConnector? connection = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);

        await using CozmoRunner runner = await CozmoRunner.ConnectAsync(
            connection,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Console.WriteLine(
            $"Connected to robot {runner.Robot.Serial} (id {runner.Robot.RobotId}).");

        if (driveOffChargerOnConnect)
        {
            try
            {
                DriveOffChargerContacts action =
                    await runner.Robot.DriveOffChargerContactsAsync().ConfigureAwait(false);
                await action.WaitForCompletedAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Warning: drive-off-charger did not complete within 10 seconds.");
            }
        }

        await program(runner.Robot).ConfigureAwait(false);
    }

    /// <summary>Waits for a time, observing the example cancellation token.</summary>
    public static Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        => Task.Delay(duration, cancellationToken);
}
