// Copyright (c) 2016 Anki, Inc.
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

// Port of examples/tutorials/01_basics/06_sing_scales.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Make Cozmo sing Do Re Mi.</summary>
public static class SingScales
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                string[] scales = ["Doe", "Ray", "Mi", "Fa", "So", "La", "Ti", "Doe"];
                double voicePitch = -1.0;
                double voicePitchDelta = 2.0 / (scales.Length - 1);

                await robot.MoveHeadAsync(-5);
                SetLiftHeight lift = await robot.SetLiftHeightAsync(0.0);
                await lift.WaitForCompletedAsync();
                SetHeadAngle head = await robot.SetHeadAngleAsync(Util.Degrees(-25.0));
                await head.WaitForCompletedAsync();

                await robot.MoveLiftAsync(0.15f);
                await robot.MoveHeadAsync(0.15f);

                foreach (string note in scales)
                {
                    SayText action = await robot.SayTextAsync(
                        note,
                        durationScalar: 0.3f,
                        voicePitch: (float)voicePitch);
                    await action.WaitForCompletedAsync();
                    voicePitch += voicePitchDelta;
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
