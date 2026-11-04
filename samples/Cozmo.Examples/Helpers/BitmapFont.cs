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

namespace Cozmo.Samples.Examples;

/// <summary>A tiny 5x7 bitmap font for the alarm-clock example.</summary>
/// <remarks>
/// The Python example uses Pillow's font renderer. The C# SDK has no font stack, so this
/// fixed font covers the characters used by the clock display.
/// </remarks>
public static class BitmapFont
{
    /// <summary>Width of one glyph in pixels.</summary>
    public const int GlyphWidth = 5;

    /// <summary>Height of one glyph in pixels.</summary>
    public const int GlyphHeight = 7;

    /// <summary>Blank columns between glyphs.</summary>
    public const int Spacing = 1;

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "11110", "00001", "00001", "10001", "01110"],
        ['6'] = ["00110", "01000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00010", "01100"],
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        [':'] = ["00000", "00100", "00100", "00000", "00100", "00100", "00000"],
        [' '] = ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
        ['-'] = ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
        ['.'] = ["00000", "00000", "00000", "00000", "00000", "00110", "00110"],
        ['?'] = ["01110", "10001", "00001", "00010", "00100", "00000", "00100"],
    };

    /// <summary>Draws text into a grayscale canvas.</summary>
    public static void DrawText(PixelCanvas canvas, string text, int x, int y, int scale = 1, byte value = 255)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(text);
        if (scale < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be at least one.");
        }

        int cursorX = x;
        foreach (char character in text.ToUpperInvariant())
        {
            if (!Glyphs.TryGetValue(character, out string[]? rows))
            {
                rows = Glyphs['?'];
            }

            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int column = 0; column < GlyphWidth; column++)
                {
                    if (rows[row][column] != '1')
                    {
                        continue;
                    }

                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            canvas.SetPixel(
                                cursorX + (column * scale) + sx,
                                y + (row * scale) + sy,
                                value);
                        }
                    }
                }
            }

            cursorX += (GlyphWidth + Spacing) * scale;
        }
    }

    /// <summary>Returns the rendered width of text in pixels.</summary>
    public static int Measure(string text, int scale = 1)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? 0 : ((text.Length * (GlyphWidth + Spacing)) - Spacing) * scale;
    }
}

/// <summary>A mutable 8-bit grayscale canvas for generated face images.</summary>
public sealed class PixelCanvas
{
    private readonly byte[] _pixels;

    /// <summary>Creates a black canvas.</summary>
    public PixelCanvas(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas dimensions must be positive.");
        }

        Width = width;
        Height = height;
        _pixels = new byte[width * height];
    }

    /// <summary>Canvas width in pixels.</summary>
    public int Width { get; }

    /// <summary>Canvas height in pixels.</summary>
    public int Height { get; }

    /// <summary>Sets one pixel, clipping coordinates outside the canvas.</summary>
    public void SetPixel(int x, int y, byte value = 255)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        _pixels[(y * Width) + x] = value;
    }

    /// <summary>Draws a one-pixel line.</summary>
    public void DrawLine(int x0, int y0, int x1, int y1, byte value = 255)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            SetPixel(x0, y0, value);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>Draws a filled rectangle.</summary>
    public void FillRectangle(int x, int y, int width, int height, byte value = 255)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                SetPixel(column, row, value);
            }
        }
    }

    /// <summary>Packs the canvas for Cozmo's OLED face display.</summary>
    public byte[] ToScreenData(bool invert = false, byte threshold = 127)
        => OledFace.ConvertImageToScreenData(_pixels, Width, Height, invert, threshold);

    /// <summary>Returns a copy of the grayscale pixels.</summary>
    public byte[] ToArray() => (byte[])_pixels.Clone();
}
