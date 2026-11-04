// Copyright (c) 2017 Anki, Inc.
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

// Port of examples/tutorials/03_vision/04_exposure.py, without the Tk viewer overlay.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Vision;

/// <summary>Demonstrate the camera's automatic and manual exposure settings.</summary>
public static class Exposure
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                await robot.Camera.SetColorImageEnabledAsync(false);
                await DemoCameraExposureAsync(robot, context.CancellationToken);

                await robot.Camera.SetColorImageEnabledAsync(true);
                await DemoCameraExposureAsync(robot, context.CancellationToken);
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }

    private static async Task DemoCameraExposureAsync(Robot robot, CancellationToken cancellationToken)
    {
        Camera camera = robot.Camera;
        CameraConfig config = camera.Config ??
            throw new InvalidOperationException("The camera configuration has not been received yet.");

        Console.WriteLine("Auto Exposure");
        await camera.EnableAutoExposureAsync(true, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        Console.WriteLine("Manual Exposure - Increasing Exposure, Fixed Gain");
        double fixedGain = (config.MinGain + config.MaxGain) * 0.5;
        for (int exposure = config.MinExposureTimeMs;
            exposure <= config.MaxExposureTimeMs;
            exposure++)
        {
            await camera.SetManualExposureAsync(exposure, fixedGain, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
        }

        Console.WriteLine("Manual Exposure - Increasing Gain, Fixed Exposure");
        const int fixedExposureMs = 10;
        for (double gain = config.MinGain; gain < config.MaxGain; gain += 0.05)
        {
            await camera.SetManualExposureAsync(fixedExposureMs, gain, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
        }

        Console.WriteLine("Mode: Auto Exposure");
        await camera.EnableAutoExposureAsync(true, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}
