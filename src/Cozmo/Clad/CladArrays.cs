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

using System;

namespace Anki.Cozmo.Sdk.Clad;

/// <summary>Helpers for CLAD array fields.</summary>
public static class CladArrays
{
    /// <summary>Creates a fixed-size array with every element default-constructed.</summary>
    public static T[] New<T>(int count)
        where T : new()
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var items = new T[count];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new T();
        }

        return items;
    }
}
