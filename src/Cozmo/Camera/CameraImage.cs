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

/// <summary>A single image from Cozmo's camera.</summary>
/// <remarks>
/// <para>
/// Port of <c>cozmo/world.py::CameraImage</c>. The Python SDK stores a decoded PIL image
/// and can annotate and resize it via <c>annotate_image</c>. This port has no imaging
/// library dependency, so <see cref="RawImage"/> is the exact JPEG byte sequence that the
/// robot produced (after the mini-JPEG header repair when applicable). Image annotation and
/// the <c>ImageAnnotator</c> hook are deliberately out of scope.
/// </para>
/// <para>
/// <see cref="Width"/> and <see cref="Height"/> describe the nominal image resolution when
/// it is known from the CLAD resolution. For minimized color frames the embedded JPEG is
/// half width; the Python SDK decodes and resizes it to the nominal size, while this port
/// leaves the bytes untouched and reports the nominal size.
/// </para>
/// </remarks>
public sealed class CameraImage
{
    /// <summary>Creates a camera image.</summary>
    /// <param name="rawImage">JPEG bytes produced by the camera.</param>
    /// <param name="imageNumber">Image number that increments for every received image.</param>
    /// <param name="width">Nominal image width in pixels, when known.</param>
    /// <param name="height">Nominal image height in pixels, when known.</param>
    /// <param name="imageRecvTime">
    /// Time the SDK received and processed the image; defaults to the current UTC time.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="rawImage"/> is <see langword="null"/>.</exception>
    public CameraImage(
        byte[] rawImage,
        int imageNumber = 0,
        int? width = null,
        int? height = null,
        DateTimeOffset? imageRecvTime = null)
    {
        ArgumentNullException.ThrowIfNull(rawImage);
        RawImage = rawImage;
        ImageNumber = imageNumber;
        Width = width;
        Height = height;
        ImageRecvTime = imageRecvTime ?? DateTimeOffset.UtcNow;
    }

    /// <summary>The raw, unprocessed JPEG bytes from the camera.</summary>
    /// <remarks>
    /// Callers must treat the array as read-only; the camera does not copy it after creating
    /// the image.
    /// </remarks>
    public byte[] RawImage { get; }

    /// <summary>Image number that increments on every new image received.</summary>
    public int ImageNumber { get; }

    /// <summary>The nominal image width in pixels, or <see langword="null"/> when unknown.</summary>
    public int? Width { get; }

    /// <summary>The nominal image height in pixels, or <see langword="null"/> when unknown.</summary>
    public int? Height { get; }

    /// <summary>The time the SDK received and processed the image.</summary>
    public DateTimeOffset ImageRecvTime { get; }
}
