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

/// <summary>Defines a bounding box within an image frame.</summary>
/// <remarks>
/// <para>Mirrors <c>ImageBox</c> in <c>cozmo/util.py</c>.</para>
/// <para>
/// Python implements this type as a <c>namedtuple</c>, so it is immutable and compares by
/// value. The readonly struct keeps those semantics and exposes <see cref="FromCladRect"/>
/// for Python's <c>_create_from_clad_rect</c> class method.
/// </para>
/// </remarks>
public readonly struct ImageBox : IEquatable<ImageBox>
{
    /// <summary>Creates an image box.</summary>
    /// <param name="topLeftX">The x coordinate of the top-left corner.</param>
    /// <param name="topLeftY">The y coordinate of the top-left corner.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="height">The height of the box.</param>
    public ImageBox(double topLeftX, double topLeftY, double width, double height)
    {
        TopLeftX = topLeftX;
        TopLeftY = topLeftY;
        Width = width;
        Height = height;
    }

    /// <summary>The x coordinate of the top-left corner.</summary>
    public double TopLeftX { get; }

    /// <summary>The y coordinate of the top-left corner.</summary>
    public double TopLeftY { get; }

    /// <summary>The width of the box.</summary>
    public double Width { get; }

    /// <summary>The height of the box.</summary>
    public double Height { get; }

    /// <summary>The x coordinate of the left of the box.</summary>
    public double LeftX => TopLeftX;

    /// <summary>The x coordinate of the right of the box.</summary>
    public double RightX => TopLeftX + Width;

    /// <summary>The y coordinate of the top of the box.</summary>
    public double TopY => TopLeftY;

    /// <summary>The y coordinate of the bottom of the box.</summary>
    public double BottomY => TopLeftY + Height;

    /// <summary>The x and y coordinates of the center of the box.</summary>
    public (double X, double Y) Center =>
        (TopLeftX + (Width * 0.5), TopLeftY + (Height * 0.5)); // util.py:83-85

    /// <summary>Creates an image box from a CLAD rectangle.</summary>
    /// <param name="imgRect">The CLAD rectangle, whose values are single-precision floats.</param>
    /// <returns>A new <see cref="ImageBox"/>.</returns>
    /// <remarks>Mirrors Python's <c>ImageBox._create_from_clad_rect</c> (util.py:55).</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="imgRect"/> is <see langword="null"/>.</exception>
    public static ImageBox FromCladRect(CladRect imgRect)
    {
        ArgumentNullException.ThrowIfNull(imgRect);
        return new ImageBox(imgRect.x_topLeft, imgRect.y_topLeft, imgRect.width, imgRect.height);
    }

    /// <summary>Scales every component of an image box.</summary>
    /// <param name="box">The box to scale.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>A new, scaled <see cref="ImageBox"/>.</returns>
    public static ImageBox operator *(ImageBox box, double scalar) =>
        new(box.TopLeftX * scalar, box.TopLeftY * scalar, box.Width * scalar, box.Height * scalar); // util.py:87-88

    /// <summary>Determines whether two image boxes are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when all four components are equal.</returns>
    public static bool operator ==(ImageBox left, ImageBox right) => left.Equals(right);

    /// <summary>Determines whether two image boxes are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when any component differs.</returns>
    public static bool operator !=(ImageBox left, ImageBox right) => !left.Equals(right);

    /// <summary>Determines whether this image box equals another image box.</summary>
    /// <param name="other">The image box to compare against.</param>
    /// <returns><see langword="true"/> when all four components are equal.</returns>
    public bool Equals(ImageBox other) =>
        TopLeftX == other.TopLeftX &&
        TopLeftY == other.TopLeftY &&
        Width == other.Width &&
        Height == other.Height;

    /// <summary>Determines whether this image box equals another object.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="ImageBox"/>.</returns>
    public override bool Equals(object? obj) => obj is ImageBox other && Equals(other);

    /// <summary>Returns a hash code for this image box.</summary>
    /// <returns>A hash code combining all four components.</returns>
    public override int GetHashCode() => HashCode.Combine(TopLeftX, TopLeftY, Width, Height);

    /// <summary>Returns a diagnostic representation of this image box.</summary>
    /// <returns>A string in the form <c>ImageBox(top_left_x=..., ...)</c>.</returns>
    public override string ToString() =>
        FormattableString.Invariant(
            $"{nameof(ImageBox)}(top_left_x={TopLeftX}, top_left_y={TopLeftY}, width={Width}, height={Height})");
}
