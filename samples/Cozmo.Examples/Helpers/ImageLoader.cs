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
using StbImageSharp;

namespace Cozmo.Samples.Examples;

/// <summary>Small image helpers used by the imaging examples.</summary>
/// <remarks>
/// The SDK itself deliberately does not decode images. These examples use the
/// StbImageSharp decoder (public domain) so that the Python examples that rely on Pillow
/// can be ported without pulling an imaging stack into the library.
/// </remarks>
public static class ImageLoader
{
    /// <summary>Loads and decodes a PNG or JPEG file.</summary>
    public static ImageData Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return FromEncodedBytes(File.ReadAllBytes(path));
    }

    /// <summary>Decodes PNG or JPEG bytes.</summary>
    public static ImageData FromEncodedBytes(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ImageResult result = ImageResult.FromMemory(encoded, ColorComponents.RedGreenBlueAlpha);
        return new ImageData(result.Width, result.Height, result.Data);
    }

    /// <summary>Decodes a camera frame from its JPEG bytes.</summary>
    public static ImageData FromCameraImage(CameraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return FromEncodedBytes(image.RawImage);
    }

    /// <summary>Resizes an image with the selected sampling mode.</summary>
    public static ImageData Resize(ImageData source, int width, int height, ResamplingMode mode)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
        }

        if (source.Width == width && source.Height == height)
        {
            return new ImageData(width, height, (byte[])source.Rgba.Clone());
        }

        return mode switch
        {
            ResamplingMode.Nearest => ResizeNearest(source, width, height),
            ResamplingMode.Bilinear => ResizeBilinear(source, width, height),
            ResamplingMode.Bicubic => ResizeBicubic(source, width, height),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown resampling mode."),
        };
    }

    /// <summary>Converts RGBA pixels to 8-bit luminance using Pillow's ITU-R 601 weights.</summary>
    public static byte[] ToGrayscale(ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var gray = new byte[image.Width * image.Height];
        for (int index = 0; index < gray.Length; index++)
        {
            int offset = index * 4;
            gray[index] = (byte)((image.Rgba[offset] * 299 + image.Rgba[offset + 1] * 587 +
                image.Rgba[offset + 2] * 114) / 1000);
        }

        return gray;
    }

    /// <summary>Mirrors an image left-to-right.</summary>
    public static ImageData FlipHorizontal(ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var flipped = new byte[image.Rgba.Length];
        int rowBytes = image.Width * 4;
        for (int y = 0; y < image.Height; y++)
        {
            int sourceRow = y * rowBytes;
            int targetRow = y * rowBytes;
            for (int x = 0; x < image.Width; x++)
            {
                int source = sourceRow + (x * 4);
                int target = targetRow + ((image.Width - 1 - x) * 4);
                flipped[target] = image.Rgba[source];
                flipped[target + 1] = image.Rgba[source + 1];
                flipped[target + 2] = image.Rgba[source + 2];
                flipped[target + 3] = image.Rgba[source + 3];
            }
        }

        return new ImageData(image.Width, image.Height, flipped);
    }

    /// <summary>Calculates the mean luminance in the range 0..255.</summary>
    public static double MeanGrayscale(ReadOnlySpan<byte> grayscale)
    {
        if (grayscale.Length == 0)
        {
            return 0.0;
        }

        long total = 0;
        foreach (byte value in grayscale)
        {
            total += value;
        }

        return (double)total / grayscale.Length;
    }

    /// <summary>Resizes, optionally mirrors, and packs an image for Cozmo's OLED face.</summary>
    /// <param name="source">Source image.</param>
    /// <param name="width">Target width; must divide the screen width.</param>
    /// <param name="height">Target height; must divide the screen height.</param>
    /// <param name="mode">Resampling mode.</param>
    /// <param name="invert">True to invert the threshold test.</param>
    /// <param name="threshold">Grayscale threshold, in 0..255.</param>
    public static byte[] ToScreenData(
        ImageData source,
        int width,
        int height,
        ResamplingMode mode,
        bool invert = false,
        byte threshold = 127)
    {
        ImageData resized = Resize(source, width, height, mode);
        byte[] grayscale = ToGrayscale(resized);
        return OledFace.ConvertImageToScreenData(grayscale, width, height, invert, threshold);
    }

    /// <summary>Packs 8-bit grayscale pixels for Cozmo's OLED face.</summary>
    public static byte[] ToScreenData(
        ReadOnlySpan<byte> grayscale,
        int width,
        int height,
        bool invert = false,
        byte threshold = 127)
        => OledFace.ConvertImageToScreenData(grayscale, width, height, invert, threshold);

    private static ImageData ResizeNearest(ImageData source, int width, int height)
    {
        var result = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            int sourceY = Math.Min(source.Height - 1, (int)((long)y * source.Height / height));
            for (int x = 0; x < width; x++)
            {
                int sourceX = Math.Min(source.Width - 1, (int)((long)x * source.Width / width));
                int sourceOffset = ((sourceY * source.Width) + sourceX) * 4;
                int targetOffset = ((y * width) + x) * 4;
                result[targetOffset] = source.Rgba[sourceOffset];
                result[targetOffset + 1] = source.Rgba[sourceOffset + 1];
                result[targetOffset + 2] = source.Rgba[sourceOffset + 2];
                result[targetOffset + 3] = source.Rgba[sourceOffset + 3];
            }
        }

        return new ImageData(width, height, result);
    }

    private static ImageData ResizeBilinear(ImageData source, int width, int height)
    {
        var result = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            double sourceY = ((y + 0.5) * source.Height / height) - 0.5;
            int y0 = (int)Math.Floor(sourceY);
            double fy = sourceY - y0;
            int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            y0 = Math.Clamp(y0, 0, source.Height - 1);

            for (int x = 0; x < width; x++)
            {
                double sourceX = ((x + 0.5) * source.Width / width) - 0.5;
                int x0 = (int)Math.Floor(sourceX);
                double fx = sourceX - x0;
                int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
                x0 = Math.Clamp(x0, 0, source.Width - 1);

                int targetOffset = ((y * width) + x) * 4;
                for (int channel = 0; channel < 4; channel++)
                {
                    double top = ((source.Rgba[((y0 * source.Width) + x0) * 4 + channel] * (1 - fx)) +
                        (source.Rgba[((y0 * source.Width) + x1) * 4 + channel] * fx));
                    double bottom = ((source.Rgba[((y1 * source.Width) + x0) * 4 + channel] * (1 - fx)) +
                        (source.Rgba[((y1 * source.Width) + x1) * 4 + channel] * fx));
                    result[targetOffset + channel] = (byte)Math.Clamp(
                        (int)Math.Round((top * (1 - fy)) + (bottom * fy)),
                        0,
                        255);
                }
            }
        }

        return new ImageData(width, height, result);
    }

    private static ImageData ResizeBicubic(ImageData source, int width, int height)
    {
        var result = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            double sourceY = ((y + 0.5) * source.Height / height) - 0.5;
            int baseY = (int)Math.Floor(sourceY);
            double fy = sourceY - baseY;

            for (int x = 0; x < width; x++)
            {
                double sourceX = ((x + 0.5) * source.Width / width) - 0.5;
                int baseX = (int)Math.Floor(sourceX);
                double fx = sourceX - baseX;

                int targetOffset = ((y * width) + x) * 4;
                for (int channel = 0; channel < 4; channel++)
                {
                    double total = 0.0;
                    double weightTotal = 0.0;
                    for (int offsetY = -1; offsetY <= 2; offsetY++)
                    {
                        double wy = CubicWeight(fy - offsetY);
                        int sampleY = Math.Clamp(baseY + offsetY, 0, source.Height - 1);
                        for (int offsetX = -1; offsetX <= 2; offsetX++)
                        {
                            double wx = CubicWeight(fx - offsetX);
                            int sampleX = Math.Clamp(baseX + offsetX, 0, source.Width - 1);
                            double weight = wx * wy;
                            total += source.Rgba[((sampleY * source.Width) + sampleX) * 4 + channel] * weight;
                            weightTotal += weight;
                        }
                    }

                    result[targetOffset + channel] = (byte)Math.Clamp(
                        (int)Math.Round(total / weightTotal),
                        0,
                        255);
                }
            }
        }

        return new ImageData(width, height, result);
    }

    private static double CubicWeight(double value)
    {
        double x = Math.Abs(value);
        if (x <= 1.0)
        {
            return (1.5 * x * x * x) - (2.5 * x * x) + 1.0;
        }

        if (x < 2.0)
        {
            return (-0.5 * x * x * x) + (2.5 * x * x) - (4.0 * x) + 2.0;
        }

        return 0.0;
    }
}
