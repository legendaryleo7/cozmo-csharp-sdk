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
using System.IO;
using Anki.Cozmo.Sdk.Clad;

namespace Cozmo.Tests;

internal static class CladTestBytes
{
    public static byte[] Pack(Action<CladBinaryWriter> pack)
    {
        using var stream = new MemoryStream();
        pack(new CladBinaryWriter(stream));
        return stream.ToArray();
    }

    public static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    public static CladBinaryReader Reader(byte[] bytes) => new(new MemoryStream(bytes));
}
