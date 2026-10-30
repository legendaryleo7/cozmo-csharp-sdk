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

/// <summary>Cozmo's OLED face display helpers: dimensions and binary screen-data packing.</summary>
/// <remarks>
/// These helpers pack grayscale pixel data into the binary format expected by the display.
/// </remarks>
public static class OledFace
{
    /// <summary>Width of the OLED screen in pixels.</summary>
    public const int ScreenWidth = 128;

    /// <summary>Half the physical screen height; the display is interlaced.</summary>
    /// <remarks>
    /// The screen shows every other line and alternates the offset to prevent burn-in, so
    /// images use half the vertical resolution.
    /// </remarks>
    public const int ScreenHalfHeight = 32;

    /// <summary>Physical height of the OLED screen in pixels (two interlaced fields).</summary>
    public const int ScreenHeight = ScreenHalfHeight * 2;

    /// <summary>Dimensions (width, height) callers should use for face images.</summary>
    public static (int Width, int Height) Dimensions => (ScreenWidth, ScreenHalfHeight);

    /// <summary>Converts binary pixel data to the packed format expected by the OLED screen.</summary>
    /// <remarks>
    /// Eight pixels are packed into each output byte, most significant bit first. Pixel
    /// values are expected to be 0 or 1. Each source pixel is repeated
    /// <c>ScreenWidth / width</c> times horizontally and each source row is repeated
    /// <c>ScreenHeight / height</c> times vertically.
    /// </remarks>
    /// <param name="pixelData">Pixel values (0 or 1), row-major, length width * height.</param>
    /// <param name="width">Width of the image described by <paramref name="pixelData"/>.</param>
    /// <param name="height">Height of the image described by <paramref name="pixelData"/>.</param>
    /// <returns>The packed screen data, 8 pixels per byte.</returns>
    /// <exception cref="ArgumentException">
    /// The data length does not match the dimensions, or a dimension is not an exact
    /// integer divisor of the screen size.
    /// </exception>
    public static byte[] ConvertPixelsToScreenData(ReadOnlySpan<byte> pixelData, int width, int height)
    {
        long expectedLength = (long)width * height;
        if (pixelData.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Invalid Dimensions: len(pixel_data) {pixelData.Length} != image_width={width} * " +
                $"image_height={height} (== {expectedLength})");
        }

        int numColumnsPerPixel = ScreenWidth / width;
        int numRowsPerPixel = ScreenHeight / height;

        if (width * numColumnsPerPixel != ScreenWidth)
        {
            throw new ArgumentException(
                $"Bad image_width: image_width {width} must be an exact integer divisor of {ScreenWidth}");
        }

        if (height * numRowsPerPixel != ScreenHeight)
        {
            throw new ArgumentException(
                $"Bad image_height: image_height {height} must be an exact integer divisor of {ScreenHeight}");
        }

        const int pixelChunksPerRow = ScreenWidth / 8;
        var resultBytes = new List<byte>(pixelData.Length * numColumnsPerPixel * numRowsPerPixel / 8);
        int x = 0;
        for (int chunkStart = 0; chunkStart + 8 <= pixelData.Length; chunkStart += 8)
        {
            int pixelByte = 0;
            for (int index = 0; index < 8; index++)
            {
                int pixel = pixelData[chunkStart + index];
                for (int repeat = 0; repeat < numColumnsPerPixel; repeat++)
                {
                    pixelByte <<= 1;
                    pixelByte += pixel;
                    x++;
                    if ((x % 8) == 0)
                    {
                        resultBytes.Add((byte)pixelByte);
                        pixelByte = 0;
                    }
                }
            }

            // At the end of a row, copy that row for every extra row per pixel.
            if (x == ScreenWidth)
            {
                x = 0;
                for (int repeat = 1; repeat < numRowsPerPixel; repeat++)
                {
                    int startOfLastRow = resultBytes.Count - pixelChunksPerRow;
                    for (int index = 0; index < pixelChunksPerRow; index++)
                    {
                        resultBytes.Add(resultBytes[startOfLastRow + index]);
                    }
                }
            }
        }

        return resultBytes.ToArray();
    }

    /// <summary>Converts grayscale pixels to the packed format expected by the OLED screen.</summary>
    /// <remarks>
    /// Pixels at or above <paramref name="threshold"/>
    /// (or at or below it when <paramref name="invert"/> is true) become 1, and everything
    /// else becomes 0.
    /// </remarks>
    /// <param name="grayscale">8-bit grayscale pixels, row-major, length width * height.</param>
    /// <param name="width">Width of the image described by <paramref name="grayscale"/>.</param>
    /// <param name="height">Height of the image described by <paramref name="grayscale"/>.</param>
    /// <param name="invert">True to turn pixels darker than the threshold on instead.</param>
    /// <param name="threshold">Grayscale threshold in the range 0..255.</param>
    /// <returns>The packed screen data, 8 pixels per byte.</returns>
    /// <exception cref="ArgumentException">
    /// The data length does not match the dimensions, or a dimension is not an exact
    /// integer divisor of the screen size.
    /// </exception>
    public static byte[] ConvertImageToScreenData(
        ReadOnlySpan<byte> grayscale,
        int width,
        int height,
        bool invert = false,
        byte threshold = 127)
    {
        var pixels = new byte[grayscale.Length];
        for (int index = 0; index < grayscale.Length; index++)
        {
            byte value = grayscale[index];
            bool on = invert ? value <= threshold : value >= threshold;
            pixels[index] = on ? (byte)1 : (byte)0;
        }

        return ConvertPixelsToScreenData(pixels, width, height);
    }
}
