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

/// <summary>Utility class that tracks a timeout condition measured from the point of construction.</summary>
/// <remarks>
/// <see cref="Start"/> is the Unix-epoch time when the helper was created, and
/// <see cref="TimeoutSeconds"/> is the configured duration. An infinite duration never
/// expires.
/// </remarks>
public sealed class TimeoutHelper
{
    /// <summary>Creates a timeout that starts measuring immediately.</summary>
    /// <param name="timeout">Seconds allotted before the timeout is met, or <see langword="null"/> for no timeout.</param>
    /// <param name="useInf">
    /// When <see langword="true"/>, <see cref="Remaining"/> returns positive infinity instead of
    /// <see langword="null"/> when <paramref name="timeout"/> is <see langword="null"/>.
    /// </param>
    public TimeoutHelper(double? timeout = null, bool useInf = false)
    {
        Start = Now();
        TimeoutSeconds = timeout;
        UseInfinity = useInf;
    }

    /// <summary>The start time in seconds since the Unix epoch.</summary>
    public double Start { get; set; }

    /// <summary>The number of seconds allotted before the timeout is met, or <see langword="null"/>.</summary>
    public double? TimeoutSeconds { get; set; }

    /// <summary>Whether <see cref="Remaining"/> returns positive infinity for an absent timeout.</summary>
    public bool UseInfinity { get; set; }

    /// <summary><see langword="true"/> when the timeout has expired.</summary>
    /// <remarks>Always <see langword="false"/> when there is no timeout.</remarks>
    public bool IsTimedOut =>
        TimeoutSeconds is double timeout && (Now() - Start) > timeout; // util.py:1004-1009

    /// <summary>The number of seconds remaining before reaching the timeout.</summary>
    /// <remarks>
    /// Never negative, even after the timer has expired. Returns <see langword="null"/> when no
    /// timeout was supplied, or positive infinity when <see cref="UseInfinity"/> is
    /// <see langword="true"/>.
    /// </remarks>
    public double? Remaining
    {
        get
        {
            if (TimeoutSeconds is not double timeout)
            {
                return UseInfinity ? double.PositiveInfinity : null; // util.py:1021-1022
            }

            double remaining = timeout - (Now() - Start);
            // Python's max(0, value) selects zero when value is negative or NaN.
            return remaining > 0.0 ? remaining : 0.0;
        }
    }

    private static double Now() => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
}
