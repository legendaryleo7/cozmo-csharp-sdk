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

/// <summary>Position of Cozmo's lift, expressed as a height.</summary>
/// <remarks>
/// Port of <c>robot.py::LiftPosition</c>. Python accepts exactly one of
/// <c>height</c>, <c>ratio</c> or <c>angle</c>; the typed factories here make that
/// choice explicit instead of raising at runtime.
/// </remarks>
public sealed class LiftPosition
{
    /// <summary>Lowest lift height above the ground, in millimetres (robot.py:105).</summary>
    public const double MinLiftHeightMillimeters = 32.0;

    /// <summary>Highest lift height above the ground, in millimetres (robot.py:111).</summary>
    public const double MaxLiftHeightMillimeters = 92.0;

    /// <summary>Length of the lift arm, in millimetres (robot.py:117).</summary>
    public const double LiftArmLengthMillimeters = 66.0;

    /// <summary>Height of the lift pivot above the ground, in millimetres (robot.py:120).</summary>
    public const double LiftPivotHeightMillimeters = 45.0;

    private readonly Distance _height;

    private LiftPosition(Distance height)
    {
        _height = height;
    }

    /// <summary>Lowest lift height as a <see cref="Distance"/> (robot.py:108).</summary>
    public static Distance MinLiftHeight => Distance.FromMillimeters(MinLiftHeightMillimeters);

    /// <summary>Highest lift height as a <see cref="Distance"/> (robot.py:114).</summary>
    public static Distance MaxLiftHeight => Distance.FromMillimeters(MaxLiftHeightMillimeters);

    /// <summary>Lift arm angle at the lowest height (robot.py:123).</summary>
    public static Angle MinLiftAngle =>
        Angle.FromRadians(Math.Asin((MinLiftHeightMillimeters - LiftPivotHeightMillimeters) / LiftArmLengthMillimeters));

    /// <summary>Lift arm angle at the highest height (robot.py:126).</summary>
    public static Angle MaxLiftAngle =>
        Angle.FromRadians(Math.Asin((MaxLiftHeightMillimeters - LiftPivotHeightMillimeters) / LiftArmLengthMillimeters));

    /// <summary>Height of the lift above the ground.</summary>
    public Distance Height => _height;

    /// <summary>Ratio from 0 (bottom) to 1 (top) that the lift is raised.</summary>
    public double Ratio =>
        (_height.DistanceMillimeters - MinLiftHeightMillimeters) /
        (MaxLiftHeightMillimeters - MinLiftHeightMillimeters);

    /// <summary>Angle of the lift arm relative to the ground.</summary>
    /// <exception cref="InvalidOperationException">The height is outside the arm's reachable range.</exception>
    public Angle Angle
    {
        get
        {
            double sine = (_height.DistanceMillimeters - LiftPivotHeightMillimeters) / LiftArmLengthMillimeters;
            if (sine < -1.0 || sine > 1.0)
            {
                throw new InvalidOperationException(
                    "The lift height is outside the range reachable by the lift arm.");
            }

            return Angle.FromRadians(Math.Asin(sine));
        }
    }

    /// <summary>Creates a position from an absolute height.</summary>
    public static LiftPosition FromHeight(Distance height) => new(height);

    /// <summary>Creates a position from a ratio between 0 (bottom) and 1 (top).</summary>
    public static LiftPosition FromRatio(double ratio)
    {
        double heightMillimeters =
            MinLiftHeightMillimeters + (ratio * (MaxLiftHeightMillimeters - MinLiftHeightMillimeters));
        return new LiftPosition(Distance.FromMillimeters(heightMillimeters));
    }

    /// <summary>Creates a position from the angle of the lift arm relative to the ground.</summary>
    public static LiftPosition FromAngle(Angle angle)
    {
        double heightMillimeters =
            (Math.Sin(angle.Radians) * LiftArmLengthMillimeters) + LiftPivotHeightMillimeters;
        return new LiftPosition(Distance.FromMillimeters(heightMillimeters));
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"LiftPosition height={_height} ratio={Ratio} angle={Angle}";
}
