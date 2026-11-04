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

using Cozmo.Samples.Examples;

// Runs one of the C# ports of the Python SDK examples.
//
//   dotnet run --project samples/Cozmo.Examples -- --list
//   dotnet run --project samples/Cozmo.Examples -- tutorials/01_basics/01_hello_world
//
// Arguments after the example key are passed through to the example.

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    PrintUsage();
    return 0;
}

if (args[0] is "--list" or "list")
{
    foreach (ExampleDefinition entry in ExampleCatalog.All)
    {
        Console.WriteLine($"{entry.Key}");
        Console.WriteLine($"    {entry.Description}");
        Console.WriteLine($"    Python: {entry.PythonPath}");
    }

    return 0;
}

string key;
string[] exampleArguments;
if (args[0] is "--example" or "-e")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("--example requires an example key. Use --list to see the available examples.");
        return 2;
    }

    key = args[1];
    exampleArguments = args[2..];
}
else
{
    key = args[0];
    exampleArguments = args[1..];
}

ExampleDefinition? example = ExampleCatalog.Find(key);
if (example is null)
{
    Console.Error.WriteLine($"Unknown example '{key}'. Use --list to see the available examples.");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
Console.WriteLine($"Running {example.Key} ({example.PythonPath})");
try
{
    await example.RunAsync(new ExampleContext(exampleArguments, cancellation.Token));
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Cancelled.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{example.Key} failed: {ex}");
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        Cozmo C# SDK examples

        Usage:
          dotnet run --project samples/Cozmo.Examples -- --list
          dotnet run --project samples/Cozmo.Examples -- <example-key> [arguments]

        Each example mirrors a Python example from cozmo-python-sdk/examples.
        The key is the Python path without the .py extension, for example
        tutorials/01_basics/01_hello_world or apps/quizmaster_cozmo.

        Examples connect to the first available Cozmo unless the example itself
        manages its connection (multi-robot and IFTTT examples). Run --list for
        the complete catalogue.
        """);
}
