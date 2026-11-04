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
using Cozmo.Samples.Examples;
using Xunit;

namespace Cozmo.Examples.Tests;

public sealed class ImageHelperTests
{
    // 128 x 32 pixels are packed, then each row is repeated for the interlaced
    // 64-line screen: 128 * 32 / 8 * 2 = 1024 bytes.
    private const int ScreenDataBytes =
        OledFace.ScreenWidth * OledFace.ScreenHeight / 8;

    [Fact]
    public void Face_image_is_resized_and_packed_for_the_oled()
    {
        ImageData image = ImageLoader.Load(ExamplePaths.FaceImage("cozmosdk.png"));
        Assert.True(image.Width > 0);
        Assert.True(image.Height > 0);
        Assert.Equal(image.Width * image.Height * 4, image.Rgba.Length);

        byte[] screenData = ImageLoader.ToScreenData(
            image,
            OledFace.ScreenWidth,
            OledFace.ScreenHalfHeight,
            ResamplingMode.Bicubic,
            invert: true);

        Assert.Equal(ScreenDataBytes, screenData.Length);
        Assert.Contains(screenData, value => value != 0);
    }

    [Fact]
    public void FlipHorizontal_mirrors_rgba_pixels()
    {
        var image = new ImageData(2, 1, [1, 2, 3, 4, 5, 6, 7, 8]);
        ImageData flipped = ImageLoader.FlipHorizontal(image);
        Assert.Equal(new byte[] { 5, 6, 7, 8, 1, 2, 3, 4 }, flipped.Rgba);
    }

    [Fact]
    public void Grayscale_uses_pillow_luminance_weights()
    {
        var image = new ImageData(1, 1, [255, 0, 0, 255]);
        byte[] gray = ImageLoader.ToGrayscale(image);
        Assert.Equal(new byte[] { 76 }, gray);
    }

    [Fact]
    public void Bitmap_font_draws_into_a_face_sized_canvas()
    {
        var canvas = new PixelCanvas(OledFace.ScreenWidth, OledFace.ScreenHalfHeight);
        BitmapFont.DrawText(canvas, "12:34:56 PM", 8, 6);
        byte[] screenData = canvas.ToScreenData();
        Assert.Equal(ScreenDataBytes, screenData.Length);
        Assert.Contains(screenData, value => value != 0);
    }

    [Fact]
    public void Resize_preserves_requested_dimensions()
    {
        var image = new ImageData(2, 2, new byte[2 * 2 * 4]);
        ImageData resized = ImageLoader.Resize(image, 4, 4, ResamplingMode.Bilinear);
        Assert.Equal(4, resized.Width);
        Assert.Equal(4, resized.Height);
        Assert.Equal(4 * 4 * 4, resized.Rgba.Length);
    }
}
