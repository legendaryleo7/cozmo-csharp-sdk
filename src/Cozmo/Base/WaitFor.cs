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

/// <summary>Helpers for waiting on several asynchronous operations at once.</summary>
/// <remarks>
/// Mirrors <c>cozmo.event.wait_for_first</c>. Python cancels the remaining futures; .NET
/// tasks cannot be cancelled without a token, so remaining tasks are left running.
/// </remarks>
public static class WaitFor
{
    /// <summary>Returns the result of the first task to complete.</summary>
    /// <exception cref="ArgumentException">No tasks were supplied.</exception>
    public static async Task<T> FirstAsync<T>(params Task<T>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Length == 0)
        {
            throw new ArgumentException("At least one task is required.", nameof(tasks));
        }

        Task<T> completed = await Task.WhenAny(tasks).ConfigureAwait(false);
        return await completed.ConfigureAwait(false);
    }

    /// <summary>Waits for the first task to complete.</summary>
    /// <exception cref="ArgumentException">No tasks were supplied.</exception>
    public static async Task FirstAsync(params Task[] tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Length == 0)
        {
            throw new ArgumentException("At least one task is required.", nameof(tasks));
        }

        Task completed = await Task.WhenAny(tasks).ConfigureAwait(false);
        await completed.ConfigureAwait(false);
    }
}
