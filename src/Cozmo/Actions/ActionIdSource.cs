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

/// <summary>Allocates action ids inside the SDK tag range, wrapping at the end.</summary>
/// <remarks>
/// The Python SDK used an unsynchronized class-level counter; this source is safe to call
/// from several threads, which matters once actions can be created outside the dispatch
/// loop.
/// </remarks>
internal static class ActionIdSource
{
    internal const uint First = (uint)global::Anki.Cozmo.ActionConstants.FIRST_SDK_TAG;
    internal const uint Last = (uint)global::Anki.Cozmo.ActionConstants.LAST_SDK_TAG;

    private static int _next = (int)First;

    /// <summary>Returns the next id and advances the counter, wrapping within the range.</summary>
    internal static uint Next()
    {
        while (true)
        {
            int current = Volatile.Read(ref _next);
            uint id = (uint)current;
            uint following = id >= Last ? First : id + 1;
            if (Interlocked.CompareExchange(ref _next, (int)following, current) == current)
            {
                return id;
            }
        }
    }

    /// <summary>Resets the counter; tests only.</summary>
    internal static void Reset(uint value)
    {
        Volatile.Write(ref _next, (int)value);
    }
}
