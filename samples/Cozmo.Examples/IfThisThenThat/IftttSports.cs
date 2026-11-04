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

// Port of examples/if_this_then_that/ifttt_sports.py.

namespace Cozmo.Samples.Examples.IfThisThenThat;

/// <summary>React when IFTTT reports an in-game sports update.</summary>
public static class IftttSports
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IfThisThenThatServer.RunAsync(
            context,
            "/iftttSports",
            "OK",
            async (json, robot) =>
            {
                string alertBody = json.GetProperty("AlertBody").GetString() ??
                    throw new InvalidOperationException("AlertBody is missing.");
                await IfThisThenThatRobot.RunReactionAsync(
                    robot,
                    alertBody,
                    "ifttt_sports.png");
            });
    }
}
