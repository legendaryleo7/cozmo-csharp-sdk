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

using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

/// <summary>Tests the WP-07 port of <c>cozmo.lights</c>.</summary>
public sealed class LightTests
{
    [Theory]
    [InlineData(0x00FF00FFu, 0x00FF00FFu)]
    [InlineData(0xFF0000FFu, 0xFF0000FFu)]
    [InlineData(0x0000FFFFu, 0x0000FFFFu)]
    [InlineData(0xFFFFFFFFu, 0xFFFFFFFFu)]
    [InlineData(0x12345678u, 0x123456FFu)]
    [InlineData(0x12345600u, 0x123456FFu)]
    [InlineData(0u, 0x000000FFu)]
    public void ColorFromIntColorForcesOpaqueAlpha(uint packed, uint expected)
    {
        // Python: Color(int_color=packed).int_color == packed | 0xff (lights.py:44).
        Assert.Equal(expected, Color.FromIntColor(packed).IntColor);
        Assert.Equal(expected, new Color(packed).IntColor);
    }

    [Theory]
    [InlineData((byte)1, (byte)2, (byte)3, 0x010203FFu)]
    [InlineData((byte)0, (byte)0, (byte)0, 0x000000FFu)]
    [InlineData((byte)255, (byte)255, (byte)255, 0xFFFFFFFFu)]
    [InlineData((byte)18, (byte)52, (byte)86, 0x123456FFu)]
    public void ColorFromRgbPacksComponents(byte red, byte green, byte blue, uint expected)
    {
        // Python: (r << 24) | (g << 16) | (b << 8) | 0xff (lights.py:46).
        Assert.Equal(expected, Color.FromRgb(red, green, blue).IntColor);
    }

    [Fact]
    public void ColorNameOnlyHasZeroValue()
    {
        // Python: Color(name="green") keeps the initial 0 (lights.py:42-47).
        Color named = Color.FromName("green");
        Assert.Equal(0u, named.IntColor);
        Assert.Equal("green", named.Name);
        Assert.Equal(default(Color), named);
    }

    [Fact]
    public void ColorComponentsExposeBytes()
    {
        Color color = Color.FromIntColor(0x12345678);

        Assert.Equal(0x12, color.R);
        Assert.Equal(0x34, color.G);
        Assert.Equal(0x56, color.B);
        Assert.Equal(0xFF, color.A);
        Assert.Equal((0x12, 0x34, 0x56), color.ToRgb());
        Assert.Equal(0x123456FFu, color.IntColor);
    }

    [Fact]
    public void ColorDefaultMatchesOff()
    {
        Color unset = default;

        Assert.Equal(0u, unset.IntColor);
        Assert.Null(unset.Name);
        Assert.Equal(Colors.Off, unset);
    }

    [Fact]
    public void ColorEqualityIgnoresName()
    {
        Color first = Color.FromIntColor(0x00FF00FF, "first");
        Color second = Color.FromIntColor(0x00FF00FF, "second");
        Color different = Color.FromIntColor(0xFF0000FF, "first");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void ColorEqualityOperatorsMatchEquals()
    {
        Color color = Color.FromRgb(0, 255, 0);
        Color same = Color.FromIntColor(0x00FF00FF, "different name");

        Assert.True(color == same);
        Assert.False(color != same);
        Assert.True(color != Color.FromRgb(255, 0, 0));
    }

    [Fact]
    public void ColorToStringUsesNameOrPackedHex()
    {
        Assert.Equal("green", Colors.Green.ToString());
        Assert.Equal("mystery", Color.FromName("mystery").ToString());
        Assert.Equal("#123456FF", Color.FromIntColor(0x12345678).ToString());
        Assert.Equal("#00000000", default(Color).ToString());
    }

    [Theory]
    [InlineData("green", 0x00FF00FFu)]
    [InlineData("red", 0xFF0000FFu)]
    [InlineData("blue", 0x0000FFFFu)]
    [InlineData("white", 0xFFFFFFFFu)]
    [InlineData("off", 0x00000000u)]
    public void NamedColorsMatchPythonSdk(string name, uint expected)
    {
        Color color = Colors.Get(name);

        Assert.Equal(expected, color.IntColor);
        Assert.Equal(name, color.Name);
    }

    [Fact]
    public void ColorsCatalogueContainsPythonColors()
    {
        Assert.Equal(5, Colors.All.Count);
        Assert.Equal(Colors.Green, Colors.All[0]);
        Assert.Equal(Colors.Red, Colors.All[1]);
        Assert.Equal(Colors.Blue, Colors.All[2]);
        Assert.Equal(Colors.White, Colors.All[3]);
        Assert.Equal(Colors.Off, Colors.All[4]);
    }

    [Fact]
    public void ColorsCatalogueLookupIgnoresCase()
    {
        Assert.True(Colors.TryGet("GREEN", out Color color));
        Assert.Equal(Colors.Green, color);
        Assert.False(Colors.TryGet("purple", out Color missing));
        Assert.Equal(default, missing);
        Assert.False(Colors.TryGet(null, out _));
        Assert.Throws<KeyNotFoundException>(() => Colors.Get("purple"));
        Assert.Throws<ArgumentNullException>(() => Colors.Get(null!));
    }

    [Fact]
    public void LightDefaultsMatchPythonSdk()
    {
        // Python defaults: off/off, 250, 0, 0, 0 (lights.py:77-78).
        var light = new Light();

        Assert.Equal(Colors.Off, light.OnColor);
        Assert.Equal(Colors.Off, light.OffColor);
        Assert.Equal("off", light.OnColor.Name);
        Assert.Equal("off", light.OffColor.Name);
        Assert.Equal(250u, light.OnPeriodMs);
        Assert.Equal(0u, light.OffPeriodMs);
        Assert.Equal(0u, light.TransitionOnPeriodMs);
        Assert.Equal(0u, light.TransitionOffPeriodMs);
    }

    [Fact]
    public void LightConstructorAcceptsZeroPeriodsLikePython()
    {
        // Python's constructor assigns directly, bypassing the setters (lights.py:79-84).
        var light = new Light(Colors.Green, Colors.Red, 0, 0, 0, 0);

        Assert.Equal(0u, light.OnPeriodMs);
        Assert.Equal(0u, light.OffPeriodMs);
        Assert.Equal(0u, light.TransitionOnPeriodMs);
        Assert.Equal(0u, light.TransitionOffPeriodMs);
    }

    [Fact]
    public void LightConstructorStoresAllValues()
    {
        var light = new Light(Colors.Green, Colors.Blue, 111, 222, 30, 40);

        Assert.Equal(Colors.Green, light.OnColor);
        Assert.Equal(Colors.Blue, light.OffColor);
        Assert.Equal(111u, light.OnPeriodMs);
        Assert.Equal(222u, light.OffPeriodMs);
        Assert.Equal(30u, light.TransitionOnPeriodMs);
        Assert.Equal(40u, light.TransitionOffPeriodMs);
    }

    [Fact]
    public void LightPeriodSettersRejectZero()
    {
        // Python raises ValueError for 0 (lights.py:113-150); a uint cannot hold 2**32.
        var light = new Light(Colors.Green, Colors.Green);

        Assert.Throws<ArgumentOutOfRangeException>(() => light.OnPeriodMs = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => light.OffPeriodMs = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => light.TransitionOnPeriodMs = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => light.TransitionOffPeriodMs = 0);

        // A rejected assignment leaves the previous value in place.
        Assert.Equal(250u, light.OnPeriodMs);
        Assert.Equal(0u, light.OffPeriodMs);
    }

    [Fact]
    public void LightPeriodSettersAcceptOneAndMaximum()
    {
        var light = new Light();

        light.OnPeriodMs = 1;
        light.OffPeriodMs = 1;
        light.TransitionOnPeriodMs = 1;
        light.TransitionOffPeriodMs = 1;
        Assert.Equal(1u, light.OnPeriodMs);
        Assert.Equal(1u, light.OffPeriodMs);
        Assert.Equal(1u, light.TransitionOnPeriodMs);
        Assert.Equal(1u, light.TransitionOffPeriodMs);

        light.OnPeriodMs = uint.MaxValue;
        light.OffPeriodMs = uint.MaxValue;
        light.TransitionOnPeriodMs = uint.MaxValue;
        light.TransitionOffPeriodMs = uint.MaxValue;
        Assert.Equal(uint.MaxValue, light.OnPeriodMs);
        Assert.Equal(uint.MaxValue, light.OffPeriodMs);
        Assert.Equal(uint.MaxValue, light.TransitionOnPeriodMs);
        Assert.Equal(uint.MaxValue, light.TransitionOffPeriodMs);
    }

    [Fact]
    public void LightColorPropertiesAreMutable()
    {
        var light = new Light();

        light.OnColor = Colors.Green;
        light.OffColor = Colors.Red;

        Assert.Equal(Colors.Green, light.OnColor);
        Assert.Equal(Colors.Red, light.OffColor);
    }

    [Fact]
    public void FlashDefaultsMatchPythonSdk()
    {
        // Python flash defaults: on 250, off 250, off colour 'off' (lights.py:152).
        Light flasher = Lights.Green.Flash();

        Assert.NotSame(Lights.Green, flasher);
        Assert.Equal(Colors.Green, flasher.OnColor);
        Assert.Equal(Colors.Off, flasher.OffColor);
        Assert.Equal("off", flasher.OffColor.Name);
        Assert.Equal(250u, flasher.OnPeriodMs);
        Assert.Equal(250u, flasher.OffPeriodMs);
        Assert.Equal(0u, flasher.TransitionOnPeriodMs);
        Assert.Equal(0u, flasher.TransitionOffPeriodMs);
    }

    [Fact]
    public void FlashUsesSuppliedValuesAndKeepsTransitions()
    {
        var source = new Light(Colors.Green, Colors.Blue, 111, 222, 30, 40);

        Light flasher = source.Flash(100, 200, Colors.Red);

        Assert.Equal(Colors.Green, flasher.OnColor);
        Assert.Equal(Colors.Red, flasher.OffColor);
        Assert.Equal(100u, flasher.OnPeriodMs);
        Assert.Equal(200u, flasher.OffPeriodMs);
        Assert.Equal(30u, flasher.TransitionOnPeriodMs);
        Assert.Equal(40u, flasher.TransitionOffPeriodMs);

        // copy.copy() leaves the source untouched (lights.py:162).
        Assert.Equal(Colors.Blue, source.OffColor);
        Assert.Equal(111u, source.OnPeriodMs);
        Assert.Equal(222u, source.OffPeriodMs);
    }

    [Fact]
    public void FlashRejectsZeroPeriods()
    {
        // flash() assigns through the setters, so zero raises just as in Python.
        Assert.Throws<ArgumentOutOfRangeException>(() => Lights.Green.Flash(offPeriodMs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lights.Green.Flash(onPeriodMs: 0));
    }

    [Fact]
    public void PredefinedLightsAreSteady()
    {
        AssertSteady(Lights.Green, Colors.Green);
        AssertSteady(Lights.Red, Colors.Red);
        AssertSteady(Lights.Blue, Colors.Blue);
        AssertSteady(Lights.White, Colors.White);
        AssertSteady(Lights.Off, Colors.Off);
    }

    [Fact]
    public void SetLightWritesBackpackSlot()
    {
        var message = new SetBackpackLEDs();
        var light = new Light(Color.FromRgb(18, 52, 86), Colors.Red, 100, 200, 30, 40);

        Lights.SetLight(message, 2, light);

        Assert.Equal(0x123456FFu, message.onColor[2]);
        Assert.Equal(0xFF0000FFu, message.offColor[2]);
        Assert.Equal(100u, message.onPeriod_ms[2]);
        Assert.Equal(200u, message.offPeriod_ms[2]);
        Assert.Equal(30u, message.transitionOnPeriod_ms[2]);
        Assert.Equal(40u, message.transitionOffPeriod_ms[2]);

        for (int i = 0; i < message.onColor.Length; i++)
        {
            if (i == 2)
            {
                continue;
            }

            Assert.Equal(0u, message.onColor[i]);
            Assert.Equal(0u, message.offColor[i]);
            Assert.Equal(0u, message.onPeriod_ms[i]);
            Assert.Equal(0u, message.offPeriod_ms[i]);
            Assert.Equal(0u, message.transitionOnPeriod_ms[i]);
            Assert.Equal(0u, message.transitionOffPeriod_ms[i]);
        }

        Assert.Equal(0, message.offset[2]);
    }

    [Fact]
    public void SetLightWritesCubeSlotAndLeavesObjectId()
    {
        var message = new SetAllActiveObjectLEDs { objectID = 42 };
        Light light = Lights.Green.Flash(123, 456, Colors.Blue);

        Lights.SetLight(message, 3, light);

        Assert.Equal(42u, message.objectID);
        Assert.Equal(0x00FF00FFu, message.onColor[3]);
        Assert.Equal(0x0000FFFFu, message.offColor[3]);
        Assert.Equal(123u, message.onPeriod_ms[3]);
        Assert.Equal(456u, message.offPeriod_ms[3]);
        Assert.Equal(0u, message.transitionOnPeriod_ms[3]);
        Assert.Equal(0u, message.transitionOffPeriod_ms[3]);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0u, message.onColor[i]);
            Assert.Equal(0u, message.offColor[i]);
            Assert.Equal(0u, message.onPeriod_ms[i]);
            Assert.Equal(0u, message.offPeriod_ms[i]);
        }
    }

    [Fact]
    public void SetLightRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Lights.SetLight((SetBackpackLEDs)null!, 0, Lights.Off));
        Assert.Throws<ArgumentNullException>(() => Lights.SetLight(new SetBackpackLEDs(), 0, null!));
        Assert.Throws<ArgumentNullException>(() => Lights.SetLight((SetAllActiveObjectLEDs)null!, 0, Lights.Off));
        Assert.Throws<ArgumentNullException>(() => Lights.SetLight(new SetAllActiveObjectLEDs(), 0, null!));
    }

    [Fact]
    public void SetLightRejectsOutOfRangeIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Lights.SetLight(new SetBackpackLEDs(), 5, Lights.Off));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lights.SetLight(new SetBackpackLEDs(), -1, Lights.Off));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lights.SetLight(new SetAllActiveObjectLEDs(), 4, Lights.Off));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lights.SetLight(new SetAllActiveObjectLEDs(), -1, Lights.Off));
    }

    [Fact]
    public void SetLightRejectsNullMessageArray()
    {
        var backpack = new SetBackpackLEDs { onColor = null! };
        var cube = new SetAllActiveObjectLEDs { transitionOffPeriod_ms = null! };

        Assert.Throws<ArgumentException>(() => Lights.SetLight(backpack, 0, Lights.Off));
        Assert.Throws<ArgumentException>(() => Lights.SetLight(cube, 0, Lights.Off));
    }

    private static void AssertSteady(Light light, Color expected)
    {
        // A steady light uses the same on/off colour; Python's glitch is disabled only then
        // (lights.py:179-195).
        Assert.Equal(expected, light.OnColor);
        Assert.Equal(expected, light.OffColor);
        Assert.Equal(250u, light.OnPeriodMs);
        Assert.Equal(0u, light.OffPeriodMs);
        Assert.Equal(0u, light.TransitionOnPeriodMs);
        Assert.Equal(0u, light.TransitionOffPeriodMs);
    }
}
