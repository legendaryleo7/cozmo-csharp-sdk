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

public sealed class ExceptionTests
{
    [Fact]
    public void SdkVersionMismatchExposesVersions()
    {
        var exception = new SdkVersionMismatchException("mismatch", "1.4.12", "3.6.6", "2.9.0");

        Assert.Equal("mismatch", exception.Message);
        Assert.Equal("1.4.12", exception.SdkVersion);
        Assert.Equal("3.6.6", exception.SdkAppVersion);
        Assert.Equal("2.9.0", exception.AppVersion);
    }

    [Fact]
    public void ExceptionHierarchyMatchesPython()
    {
        Assert.True(typeof(SdkVersionMismatchException).IsSubclassOf(typeof(CozmoConnectionException)));
        Assert.True(typeof(ConnectionAbortedException).IsSubclassOf(typeof(CozmoConnectionException)));
        Assert.True(typeof(ConnectionCheckFailedException).IsSubclassOf(typeof(CozmoConnectionException)));
        Assert.True(typeof(NoDevicesFoundException).IsSubclassOf(typeof(CozmoConnectionException)));
        Assert.True(typeof(NotPickupableException).IsSubclassOf(typeof(ActionException)));
        Assert.True(typeof(CannotPlaceObjectsOnThisException).IsSubclassOf(typeof(ActionException)));
        Assert.True(typeof(RobotBusyException).IsSubclassOf(typeof(ActionException)));
        Assert.True(typeof(StopPropagationException).IsSubclassOf(typeof(CozmoSdkException)));
        Assert.True(typeof(SdkShutdownException).IsSubclassOf(typeof(CozmoSdkException)));
        Assert.True(typeof(AnimationsNotLoadedException).IsSubclassOf(typeof(CozmoSdkException)));
    }
}
