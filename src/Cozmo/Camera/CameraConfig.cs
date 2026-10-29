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

using CladCameraConfig = Anki.Cozmo.ExternalInterface.CameraConfig;

namespace Anki.Cozmo.Sdk;

/// <summary>The fixed properties and calibration of Cozmo's camera.</summary>
/// <remarks>
/// Port of <c>cozmo/camera.py::CameraConfig</c>. The focal length and optical center are
/// expressed in floating point pixel values. Together they form the calibration matrix:
/// <code>
/// +--------------+--------------+---------------+
/// | FocalLength.X|      0       |    Center.X   |
/// +--------------+--------------+---------------+
/// |       0      | FocalLength.Y|    Center.Y   |
/// +--------------+--------------+---------------+
/// |       0      |       0      |        1      |
/// +--------------+--------------+---------------+
/// </code>
/// </remarks>
public sealed class CameraConfig
{
    /// <summary>Creates a camera configuration from calibrated values.</summary>
    /// <param name="focalLengthX">Focal length combined with pixel skew along x.</param>
    /// <param name="focalLengthY">Focal length combined with pixel skew along y.</param>
    /// <param name="centerX">x coordinate of the optical center of projection.</param>
    /// <param name="centerY">y coordinate of the optical center of projection.</param>
    /// <param name="fovXDegrees">Horizontal field of view, in degrees.</param>
    /// <param name="fovYDegrees">Vertical field of view, in degrees.</param>
    /// <param name="minExposureTimeMs">Minimum supported exposure time in milliseconds.</param>
    /// <param name="maxExposureTimeMs">Maximum supported exposure time in milliseconds.</param>
    /// <param name="minGain">Minimum supported camera gain.</param>
    /// <param name="maxGain">Maximum supported camera gain.</param>
    public CameraConfig(
        double focalLengthX,
        double focalLengthY,
        double centerX,
        double centerY,
        double fovXDegrees,
        double fovYDegrees,
        int minExposureTimeMs,
        int maxExposureTimeMs,
        double minGain,
        double maxGain)
    {
        FocalLength = new Vector2(focalLengthX, focalLengthY);
        Center = new Vector2(centerX, centerY);
        FovX = Angle.FromDegrees(fovXDegrees);
        FovY = Angle.FromDegrees(fovYDegrees);
        MinExposureTimeMs = minExposureTimeMs;
        MaxExposureTimeMs = maxExposureTimeMs;
        MinGain = minGain;
        MaxGain = maxGain;
    }

    /// <summary>The focal length of the camera, in pixels.</summary>
    /// <remarks>
    /// This is focal length combined with pixel skew (the pixels are not perfectly square),
    /// so the x and y values differ subtly, for example <c>&lt;288.87, 288.36&gt;</c>.
    /// </remarks>
    public Vector2 FocalLength { get; }

    /// <summary>The optical center of projection within the image, in pixels.</summary>
    /// <remarks>
    /// Close to the center of the image, adjusted for the factory calibration of the lens,
    /// for example <c>&lt;155.11, 111.40&gt;</c>.
    /// </remarks>
    public Vector2 Center { get; }

    /// <summary>The horizontal (x) field of view.</summary>
    public Angle FovX { get; }

    /// <summary>The vertical (y) field of view.</summary>
    public Angle FovY { get; }

    /// <summary>The minimum supported exposure time in milliseconds.</summary>
    public int MinExposureTimeMs { get; }

    /// <summary>The maximum supported exposure time in milliseconds.</summary>
    public int MaxExposureTimeMs { get; }

    /// <summary>The minimum supported camera gain.</summary>
    public double MinGain { get; }

    /// <summary>The maximum supported camera gain.</summary>
    public double MaxGain { get; }

    /// <summary>Creates a configuration from the robot's CLAD camera settings.</summary>
    /// <param name="cladConfig">Camera configuration received from the robot.</param>
    /// <returns>The equivalent camera configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cladConfig"/> is <see langword="null"/>.</exception>
    public static CameraConfig FromClad(CladCameraConfig cladConfig)
    {
        ArgumentNullException.ThrowIfNull(cladConfig);
        return new CameraConfig(
            cladConfig.focalLengthX,
            cladConfig.focalLengthY,
            cladConfig.centerX,
            cladConfig.centerY,
            cladConfig.fovX,
            cladConfig.fovY,
            cladConfig.minCameraExposureTime_ms,
            cladConfig.maxCameraExposureTime_ms,
            cladConfig.minCameraGain,
            cladConfig.maxCameraGain);
    }
}
