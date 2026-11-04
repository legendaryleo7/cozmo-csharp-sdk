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

// Port of examples/tools/cubes/auto_cube_disconnect.py.

namespace Cozmo.Samples.Examples.Tools.Cubes;

/// <summary>Enable or disable automatic cube disconnection after each program.</summary>
public static class AutoCubeDisconnect
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool? enable = ParseEnable(context.Arguments);
        if (enable is null)
        {
            Console.WriteLine("Incorrect options provided, see:");
            Console.WriteLine("  -e, --enable    Enable auto cube disconnection");
            Console.WriteLine("  -d, --disable   Disable auto cube disconnection");
            return Task.CompletedTask;
        }

        return ExampleRunner.RunAsync(
            async robot =>
            {
                await robot.World.AutoDisconnectFromCubesAtEndAsync(enable.Value);
                Console.WriteLine(enable.Value
                    ? "Automatic cube disconnection enabled."
                    : "Automatic cube disconnection disabled.");
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }

    private static bool? ParseEnable(string[] args)
    {
        foreach (string argument in args)
        {
            switch (argument)
            {
                case "-e":
                case "--enable":
                    return true;
                case "-d":
                case "--disable":
                    return false;
            }
        }

        return null;
    }
}
