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
using Anki.Cozmo.Sdk.Clad;

namespace Cozmo.Tests;

public sealed class CladIdentityTests
{
    [Fact]
    public void PackageMatchesCozmocladReleasedWithSdk()
    {
        Assert.Equal("3.6.0", CladIdentity.PackageVersion);
        Assert.Equal("00003.00006.00000", CladIdentity.BuildVersion);
        Assert.Equal(SdkVersion.RequiredCladVersion, "3.6.6");
    }

    [Fact]
    public void HashesAreSixteenBytesInWireOrder()
    {
        // cozmoclad stores the hashes as ints and compares them as little-endian bytes.
        Assert.Equal(
            new byte[] { 0xda, 0x3e, 0x90, 0xa6, 0x72, 0x4a, 0xdf, 0x4b, 0x5e, 0x7c, 0xf5, 0x76, 0x4e, 0x96, 0x00, 0x96 },
            CladIdentity.EngineToGameHash);
        Assert.Equal(
            new byte[] { 0x3a, 0x30, 0xc4, 0x20, 0xab, 0xd1, 0xb2, 0x57, 0x39, 0x2c, 0x9a, 0x5f, 0xb0, 0xca, 0xbb, 0x4e },
            CladIdentity.GameToEngineHash);
    }

    [Fact]
    public void GeneratedBuildVersionSatisfiesSdkMinimum()
    {
        SdkVersion.VerifyMinCladVersion(CladIdentity.BuildVersion);
    }

    [Fact]
    public void TopLevelUnionsUseTwoByteTags()
    {
        // cozmoclad 3.6.6 writes the union tag as a uint16; the old DLL used one byte.
        var union = new Anki.Cozmo.ExternalInterface.MessageGameToEngine
        {
            StopAllAudioEvents = new Anki.AudioEngine.Multiplexer.StopAllAudioEvents(),
        };

        byte[] bytes = CladTestBytes.Pack(union.Pack);

        Assert.Equal(new byte[] { 0x02, 0x00 }, bytes.Take(2));
    }
}
