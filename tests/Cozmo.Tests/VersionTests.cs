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

namespace Cozmo.Tests;

public sealed class VersionTests
{
    [Fact]
    public void VersionConstantsMatchPythonSdk()
    {
        Assert.Equal("1.4.12", SdkVersion.Version);
        Assert.Equal("3.6.6", SdkVersion.RequiredCladVersion);
        Assert.Equal("2.0.0", SdkVersion.MinCladVersion);
    }

    [Theory]
    [InlineData("00003.00006.00000")]
    [InlineData("00002.00000.00000")]
    [InlineData("3.6.0")]
    [InlineData("10.0.0")]
    public void SupportedCladVersionsPass(string installed)
    {
        Assert.True(SdkVersion.IsCladVersionSupported(installed));
        SdkVersion.VerifyMinCladVersion(installed);
    }

    [Theory]
    [InlineData("00001.00009.00009")]
    [InlineData("1.9.9")]
    [InlineData("0.0.1")]
    public void OldCladVersionsFail(string installed)
    {
        Assert.False(SdkVersion.IsCladVersionSupported(installed));
        Assert.Throws<CozmoSdkException>(() => SdkVersion.VerifyMinCladVersion(installed));
    }

    [Fact]
    public void NumericComparisonHandlesMultiDigitSegments()
    {
        // A naive lexical compare would call "1.10.0" newer than "2.0.0".
        Assert.False(SdkVersion.IsCladVersionSupported("1.10.0"));
        Assert.True(SdkVersion.IsCladVersionSupported("10.0.0"));
    }
}
