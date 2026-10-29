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

public sealed class CladEnumTests
{
    [Fact]
    public void FromNameReturnsEntry()
    {
        CladEnum<TestColor> entry = CladEnum<TestColor>.FromName("Green");

        Assert.Equal("Green", entry.Name);
        Assert.Equal(TestColor.Green, entry.Value);
        Assert.Equal(2, entry.Id);
        Assert.Equal("Green", entry.ToString());
    }

    [Fact]
    public void FromIdReturnsEntry()
    {
        CladEnum<TestColor> entry = CladEnum<TestColor>.FromId(3);

        Assert.Equal(TestColor.Blue, entry.Value);
    }

    [Fact]
    public void FromValueReturnsEntry()
    {
        Assert.Equal("Red", CladEnum<TestColor>.FromValue(TestColor.Red).Name);
    }

    [Fact]
    public void AllListsEveryEntryInDeclarationOrder()
    {
        Assert.Equal(3, CladEnum<TestColor>.All.Count);
        Assert.Equal(new[] { "Red", "Green", "Blue" }, CladEnum<TestColor>.All.Select(entry => entry.Name));
    }

    [Fact]
    public void TryFromIdReportsMissingEntry()
    {
        Assert.True(CladEnum<TestColor>.TryFromId(1, out CladEnum<TestColor>? found));
        Assert.Equal("Red", found.Name);
        Assert.False(CladEnum<TestColor>.TryFromId(99, out CladEnum<TestColor>? missing));
        Assert.Null(missing);
    }

    [Fact]
    public void UnknownNameThrows()
    {
        Assert.Throws<KeyNotFoundException>(() => CladEnum<TestColor>.FromName("Purple"));
        Assert.Throws<KeyNotFoundException>(() => CladEnum<TestColor>.FromId(99));
    }

    [Fact]
    public void EntriesCompareByValue()
    {
        Assert.Equal(CladEnum<TestColor>.FromName("Red"), CladEnum<TestColor>.FromId(1));
        Assert.NotEqual(CladEnum<TestColor>.FromName("Red"), CladEnum<TestColor>.FromName("Blue"));
        Assert.True(CladEnum<TestColor>.FromName("Red") == CladEnum<TestColor>.FromId(1));
    }

    private enum TestColor : byte
    {
        Red = 1,
        Green = 2,
        Blue = 3,
    }
}
