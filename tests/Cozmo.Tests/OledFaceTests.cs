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

/// <summary>
/// OLED packing tests. Every expected byte string below was produced by the Python SDK's
/// <c>cozmo/oled_face.py</c> functions and is stored here as a literal.
/// </summary>
public sealed class OledFaceTests
{
    [Fact]
    public void DimensionsMatchPython()
    {
        Assert.Equal((128, 32), OledFace.Dimensions);
        Assert.Equal(128, OledFace.ScreenWidth);
        Assert.Equal(32, OledFace.ScreenHalfHeight);
        Assert.Equal(64, OledFace.ScreenHeight);
    }

    [Fact]
    public void FullResolutionPatternMatchesPython()
    {
        byte[] pixels = Pixels(128, 64, (x, y) => (byte)((x + y) % 3 == 0 ? 1 : 0));
        byte[] expected = Hex(
                "9249249249249249249249249249249224924924924924924924924924924924492492492492492492492492492492499249" +
                "2492492492492492492492492492249249249249249249249249249249244924924924924924924924924924924992492492" +
                "4924924924924924924924922492492492492492492492492492492449249249249249249249249249249249924924924924" +
                "9249249249249249249224924924924924924924924924924924492492492492492492492492492492499249249249249249" +
                "2492492492492492249249249249249249249249249249244924924924924924924924924924924992492492492492492492" +
                "4924924924922492492492492492492492492492492449249249249249249249249249249249924924924924924924924924" +
                "9249249224924924924924924924924924924924492492492492492492492492492492499249249249249249249249249249" +
                "2492249249249249249249249249249249244924924924924924924924924924924992492492492492492492492492492492" +
                "2492492492492492492492492492492449249249249249249249249249249249924924924924924924924924924924922492" +
                "4924924924924924924924924924492492492492492492492492492492499249249249249249249249249249249224924924" +
                "9249249249249249249249244924924924924924924924924924924992492492492492492492492492492492249249249249" +
                "2492492492492492492449249249249249249249249249249249924924924924924924924924924924922492492492492492" +
                "4924924924924924492492492492492492492492492492499249249249249249249249249249249224924924924924924924" +
                "9249249249244924924924924924924924924924924992492492492492492492492492492492249249249249249249249249" +
                "2492492449249249249249249249249249249249924924924924924924924924924924922492492492492492492492492492" +
                "4924492492492492492492492492492492499249249249249249249249249249249224924924924924924924924924924924" +
                "4924924924924924924924924924924992492492492492492492492492492492249249249249249249249249249249244924" +
                "9249249249249249249249249249924924924924924924924924924924922492492492492492492492492492492449249249" +
                "2492492492492492492492499249249249249249249249249249249224924924924924924924924924924924492492492492" +
                "4924924924924924924992492492492492492492492492492492249249249249249249249249249249244924924924924924" +
                "924924924924924992492492492492492492492492492492");

        byte[] actual = OledFace.ConvertPixelsToScreenData(pixels, 128, 64);

        Assert.Equal(1024, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UpscaledPatternMatchesPython()
    {
        byte[] pixels = Pixels(16, 8, (x, y) => (byte)((x * 3 + y * 5) % 7 < 3 ? 1 : 0));
        byte[] expected = Hex(
                "ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff00" +
                "00ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff" +
                "00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff0000ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000" +
                "ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00" +
                "ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00" +
                "ff0000ff00ff0000ff00ff00ff0000ff00ff00ff00000000ff00ff00ff0000ff00ff00ff00000000ff00ff00ff0000ff00ff" +
                "00ff00000000ff00ff00ff0000ff00ff00ff00000000ff00ff00ff0000ff00ff00ff00000000ff00ff00ff0000ff00ff00ff" +
                "00000000ff00ff00ff0000ff00ff00ff00000000ff00ff00ff0000ff00ff00ff0000ff00ff0000ff00ff00ff0000ff00ff00" +
                "ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00" +
                "ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff00" +
                "00ff00ff00ff0000ff00ff0000ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff" +
                "0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000" +
                "ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff0000ff00ff00ff00" +
                "00ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff" +
                "00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff" +
                "00ff00ff0000ff00ff00ff0000ff00ff00ffff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00" +
                "ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00" +
                "ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff0000ff" +
                "00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff" +
                "00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00ff0000ff00ff00ff" +
                "0000ff00ff00ff00ff0000ff00ff00ff0000ff00ff00ff00");

        byte[] actual = OledFace.ConvertPixelsToScreenData(pixels, 16, 8);

        Assert.Equal(1024, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SmallPatternMatchesPython()
    {
        byte[] pixels = Pixels(8, 8, (x, y) => (byte)((x * 7 + y * 11 + x * y) % 5 < 2 ? 1 : 0));
        byte[] expected = Hex(
                "ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff" +
                "00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff0000" +
                "0000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000" +
                "ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff" +
                "0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000" +
                "ffff000000000000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000" +
                "ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffff" +
                "ffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff00000000000000000000000000000000" +
                "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
                "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
                "0000000000000000000000000000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff" +
                "000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff0000" +
                "00000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffffffff00000000ffff0000" +
                "ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff" +
                "00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff0000" +
                "0000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000" +
                "ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff" +
                "00000000ffff0000ffff00000000ffff00000000ffff0000ffff00000000ffff00000000ffff0000ffff000000000000ffff" +
                "ffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff" +
                "000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff000000000000ffffffff0000ffffffff0000" +
                "00000000ffffffff0000ffffffff000000000000ffffffff");

        byte[] actual = OledFace.ConvertPixelsToScreenData(pixels, 8, 8);

        Assert.Equal(1024, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GrayscaleImageMatchesPython()
    {
        byte[] grayscale = Pixels(8, 8, (x, y) => (byte)((x * 29 + y * 47 + 13) % 256));
        byte[] expected = Hex(
                "0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000" +
                "000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff00000000" +
                "00000000ffffffffffffffff0000000000000000ffffffffffffffff000000000000ffffffffffffffff0000000000000000" +
                "ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffff" +
                "ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffff" +
                "ffffffff00000000ffffffffffffffffffff000000000000ffffffffffffffffffff000000000000ffffffffffffffffffff" +
                "000000000000ffffffffffffffffffff000000000000ffffffffffffffffffff000000000000ffffffffffffffffffff0000" +
                "00000000ffffffffffffffffffff000000000000ffffffffffffffffffff00000000ffffffffffffffff0000000000000000" +
                "ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffff" +
                "ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffff" +
                "ffffffff0000000000000000ffffffff00000000000000000000ffffffffffff00000000000000000000ffffffffffff0000" +
                "0000000000000000ffffffffffff00000000000000000000ffffffffffff00000000000000000000ffffffffffff00000000" +
                "000000000000ffffffffffff00000000000000000000ffffffffffff00000000000000000000ffffffff0000000000000000" +
                "ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffff" +
                "ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffff" +
                "ffffffff0000000000000000ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff" +
                "0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000" +
                "000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff00000000" +
                "ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffff" +
                "ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffff" +
                "ffffffff0000000000000000ffffffffffffffff00000000");

        byte[] actual = OledFace.ConvertImageToScreenData(grayscale, 8, 8);

        Assert.Equal(1024, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InvertedGrayscaleImageMatchesPython()
    {
        byte[] grayscale = Pixels(8, 8, (x, y) => (byte)((x * 29 + y * 47 + 13) % 256));
        byte[] expected = Hex(
                "ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffff" +
                "ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffff" +
                "ffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffff0000000000000000ffffffffffffffff" +
                "0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000" +
                "000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff00000000" +
                "00000000ffffffff00000000000000000000ffffffffffff00000000000000000000ffffffffffff00000000000000000000" +
                "ffffffffffff00000000000000000000ffffffffffff00000000000000000000ffffffffffff00000000000000000000ffff" +
                "ffffffff00000000000000000000ffffffffffff00000000000000000000ffffffff0000000000000000ffffffffffffffff" +
                "0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000" +
                "000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff00000000" +
                "00000000ffffffffffffffff00000000ffffffffffffffffffff000000000000ffffffffffffffffffff000000000000ffff" +
                "ffffffffffffffff000000000000ffffffffffffffffffff000000000000ffffffffffffffffffff000000000000ffffffff" +
                "ffffffffffff000000000000ffffffffffffffffffff000000000000ffffffffffffffffffff00000000ffffffffffffffff" +
                "0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000" +
                "000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff00000000" +
                "00000000ffffffffffffffff000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000" +
                "ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffff" +
                "ffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffff" +
                "0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000" +
                "000000000000ffffffffffffffff0000000000000000ffffffffffffffff0000000000000000ffffffffffffffff00000000" +
                "00000000ffffffffffffffff0000000000000000ffffffff");

        byte[] actual = OledFace.ConvertImageToScreenData(grayscale, 8, 8, invert: true);

        Assert.Equal(1024, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThresholdIsInclusive()
    {
        byte[] atThreshold = Enumerable.Repeat((byte)127, 8 * 8).ToArray();
        byte[] belowThreshold = Enumerable.Repeat((byte)126, 8 * 8).ToArray();

        byte[] on = OledFace.ConvertImageToScreenData(atThreshold, 8, 8);
        byte[] off = OledFace.ConvertImageToScreenData(belowThreshold, 8, 8);

        Assert.Equal(1024, on.Length);
        Assert.All(on, value => Assert.Equal(0xFF, value));
        Assert.All(off, value => Assert.Equal(0x00, value));
    }

    [Fact]
    public void InvertedThresholdIsInclusive()
    {
        byte[] atThreshold = Enumerable.Repeat((byte)127, 8 * 8).ToArray();
        byte[] aboveThreshold = Enumerable.Repeat((byte)128, 8 * 8).ToArray();

        byte[] on = OledFace.ConvertImageToScreenData(atThreshold, 8, 8, invert: true);
        byte[] off = OledFace.ConvertImageToScreenData(aboveThreshold, 8, 8, invert: true);

        Assert.All(on, value => Assert.Equal(0xFF, value));
        Assert.All(off, value => Assert.Equal(0x00, value));
    }

    [Fact]
    public void CustomThresholdSelectsPixels()
    {
        byte[] grayscale = Enumerable.Repeat((byte)200, 8 * 8).ToArray();

        byte[] below = OledFace.ConvertImageToScreenData(grayscale, 8, 8, threshold: 201);
        byte[] at = OledFace.ConvertImageToScreenData(grayscale, 8, 8, threshold: 200);

        Assert.All(below, value => Assert.Equal(0x00, value));
        Assert.All(at, value => Assert.Equal(0xFF, value));
    }

    [Fact]
    public void MismatchedDataLengthThrows()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OledFace.ConvertPixelsToScreenData(new byte[5], 2, 3));

        Assert.Equal(
            "Invalid Dimensions: len(pixel_data) 5 != image_width=2 * image_height=3 (== 6)",
            exception.Message);
    }

    [Fact]
    public void NonDivisorWidthThrows()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OledFace.ConvertPixelsToScreenData(new byte[100 * 64], 100, 64));

        Assert.Equal(
            "Bad image_width: image_width 100 must be an exact integer divisor of 128",
            exception.Message);
    }

    [Fact]
    public void NonDivisorHeightThrows()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OledFace.ConvertPixelsToScreenData(new byte[128 * 3], 128, 3));

        Assert.Equal(
            "Bad image_height: image_height 3 must be an exact integer divisor of 64",
            exception.Message);
    }

    [Fact]
    public void WidthIsValidatedBeforeHeight()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OledFace.ConvertPixelsToScreenData(new byte[100 * 3], 100, 3));

        Assert.StartsWith("Bad image_width:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImageConversionValidatesDimensions()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OledFace.ConvertImageToScreenData(new byte[3], 2, 2));

        Assert.StartsWith("Invalid Dimensions:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImageConversionValidatesWidth()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OledFace.ConvertImageToScreenData(new byte[100 * 64], 100, 64));

        Assert.Equal(
            "Bad image_width: image_width 100 must be an exact integer divisor of 128",
            exception.Message);
    }

    private static byte[] Pixels(int width, int height, Func<int, int, byte> pixel)
    {
        var data = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                data[(y * width) + x] = pixel(x, y);
            }
        }

        return data;
    }

    private static byte[] Hex(string value) => Convert.FromHexString(value);
}
