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

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples;

/// <summary>Shared body of the two cube-blinking examples.</summary>
internal static class CubeBlinker
{
    public static Task RunAsync(ExampleContext context)
    {
        return ExampleRunner.RunAsync(
            async robot =>
            {
                Behavior lookAround = await robot.StartBehaviorAsync(BehaviorTypes.LookAroundInPlace);
                LightCube? cube;
                try
                {
                    cube = await robot.World.WaitForObservedLightCubeAsync(TimeSpan.FromSeconds(60));
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Didn't find a cube :-(");
                    return;
                }
                finally
                {
                    await lookAround.StopAsync();
                }

                await using var chaser = new LightChaser(
                    cube,
                    step =>
                    {
                        Light[] corners = [Lights.Off, Lights.Off, Lights.Off, Lights.Off];
                        corners[step % 4] = Lights.Green;
                        return corners;
                    });
                chaser.Start(TimeSpan.FromSeconds(0.1));

                try
                {
                    Console.WriteLine("Waiting for cube to be tapped");
                    await cube.WaitForTapAsync(TimeSpan.FromSeconds(10));
                    Console.WriteLine("Cube tapped");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("No-one tapped our cube :-(");
                }
                finally
                {
                    await chaser.StopAsync();
                    await cube.SetLightsOffAsync();
                }
            },
            cancellationToken: context.CancellationToken);
    }
}
