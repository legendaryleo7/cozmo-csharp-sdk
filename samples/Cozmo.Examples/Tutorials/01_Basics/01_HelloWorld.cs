// Copyright (c) 2016 Anki, Inc.
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

// Port of examples/tutorials/01_basics/01_hello_world.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.Basics;

/// <summary>Make Cozmo say "Hello World".</summary>
public static class HelloWorld
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                SayText hello = await robot.SayTextAsync("Hello World");
                await hello.WaitForCompletedAsync();
            },
            cancellationToken: context.CancellationToken);
    }
}
