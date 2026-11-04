// Copyright (c) 2016-2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
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

/// <summary>Puts Cozmo's lift down and head up so the face is easy to see.</summary>
public static class FacePositioning
{
    /// <summary>Moves the lift and head only when they are not already in position.</summary>
    public static async Task GetInPositionAsync(Robot robot)
    {
        ArgumentNullException.ThrowIfNull(robot);
        bool liftTooHigh = robot.LiftHeight.DistanceMillimeters > 45.0;
        bool headTooLow = (robot.HeadAngle?.Degrees ?? 0.0) < 40.0;
        if (!liftTooHigh && !headTooLow)
        {
            return;
        }

        await using PerformOffChargerContext charger = await robot.PerformOffChargerAsync();
        SetLiftHeight lift = await robot.SetLiftHeightAsync(0.0, inParallel: true);
        SetHeadAngle head = await robot.SetHeadAngleAsync(SetHeadAngle.MaxHeadAngle, inParallel: true);
        await lift.WaitForCompletedAsync();
        await head.WaitForCompletedAsync();
    }
}
