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

// Port of examples/tutorials/06_actions/01_parallel_actions.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Actions;

/// <summary>Run actions in parallel, detect conflicts, and abort actions.</summary>
public static class ParallelActions
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                (int width, int height) = OledFace.Dimensions;
                byte[] face = ImageLoader.ToScreenData(
                    ImageLoader.Load(ExamplePaths.FaceImage("cozmosdk.png")),
                    width,
                    height,
                    ResamplingMode.Bicubic,
                    invert: true);

                await Example1LiftHeadAsync(robot);
                await Example2ConflictingActionsAsync(robot);
                await Example3AbortOneActionAsync(robot, face, context.CancellationToken);
                await Example4AbortAllActionsAsync(robot, face, context.CancellationToken);
                Console.WriteLine("----------------------------------------");
            },
            cancellationToken: context.CancellationToken);
    }

    private static async Task Example1LiftHeadAsync(Robot robot)
    {
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("Example 1: Move Lift and Head in Parallel");
        Console.WriteLine("First, move the lift and head down, in sequence.");
        SetHeadAngle down = await robot.SetHeadAngleAsync(SetHeadAngle.MinHeadAngle);
        await down.WaitForCompletedAsync();
        SetLiftHeight bottom = await robot.SetLiftHeightAsync(0.0);
        await bottom.WaitForCompletedAsync();

        Console.WriteLine("Now, move the lift and head back up, in parallel.");
        SetHeadAngle up = await robot.SetHeadAngleAsync(SetHeadAngle.MaxHeadAngle);
        SetLiftHeight lift = await robot.SetLiftHeightAsync(1.0, inParallel: true);
        await up.WaitForCompletedAsync();
        await lift.WaitForCompletedAsync();
        Console.WriteLine($"action1 = {up}");
        Console.WriteLine($"action2 = {lift}");
    }

    private static async Task Example2ConflictingActionsAsync(Robot robot)
    {
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("Example 2: Conflicting actions.");
        Console.WriteLine(
            "Try to drive straight and turn in parallel; the second action should fail " +
            "with tracks_locked.");
        DriveStraight drive = await robot.DriveStraightAsync(
            Distance.FromMillimeters(50),
            Speed.FromMillimetersPerSecond(25),
            shouldPlayAnim: false,
            inParallel: true);
        TurnInPlace turn = await robot.TurnInPlaceAsync(Util.Degrees(90), inParallel: true);
        await turn.WaitForCompletedAsync();
        Console.WriteLine($"action2 = {turn} result={turn.Result?.Name}");
        await drive.WaitForCompletedAsync();
        Console.WriteLine($"action1 = {drive} result={drive.Result?.Name}");
    }

    private static async Task Example3AbortOneActionAsync(
        Robot robot,
        byte[] face,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("Example 3: Abort some parallel actions.");
        SetLiftHeight lift = await robot.SetLiftHeightAsync(0.0, inParallel: true);
        SetHeadAngle head = await robot.SetHeadAngleAsync(
            SetHeadAngle.MinHeadAngle,
            duration: 6.0f,
            inParallel: true);
        DriveStraight drive = await robot.DriveStraightAsync(
            Distance.FromMillimeters(75),
            Speed.FromMillimetersPerSecond(25),
            shouldPlayAnim: false,
            inParallel: true);
        DisplayOledFaceImage faceAction = await robot.DisplayOledFaceImageAsync(face, 30000);

        await lift.AbortAsync(logAbortMessages: true);
        await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
        await head.AbortAsync(logAbortMessages: true);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        await faceAction.AbortAsync(logAbortMessages: true);

        await drive.WaitForCompletedAsync();
        Console.WriteLine($"action1 = {lift}");
        Console.WriteLine($"action2 = {head}");
        Console.WriteLine($"action3 = {drive}");
        Console.WriteLine($"action4 = {faceAction}");
    }

    private static async Task Example4AbortAllActionsAsync(
        Robot robot,
        byte[] face,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("Example 4: Abort all parallel actions.");
        SetLiftHeight lift = await robot.SetLiftHeightAsync(
            0.0,
            duration: 6.0f,
            inParallel: true);
        SetHeadAngle head = await robot.SetHeadAngleAsync(
            SetHeadAngle.MaxHeadAngle,
            duration: 6.0f,
            inParallel: true);
        DriveStraight drive = await robot.DriveStraightAsync(
            Distance.FromMillimeters(75),
            Speed.FromMillimetersPerSecond(25),
            shouldPlayAnim: false,
            inParallel: true);
        DisplayOledFaceImage faceAction = await robot.DisplayOledFaceImageAsync(face, 30000);

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        await robot.AbortAllActionsAsync(logAbortMessages: true);
        await robot.ActionDispatcher.WaitForAllActionsCompletedAsync();
        Console.WriteLine($"action1 = {lift}");
        Console.WriteLine($"action2 = {head}");
        Console.WriteLine($"action3 = {drive}");
        Console.WriteLine($"action4 = {faceAction}");
    }
}
