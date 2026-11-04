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

// Port of examples/if_this_then_that/ifttt_sports_flask.py.
//
// The Python version demonstrates a synchronous Flask server plus a worker thread and a
// queue. The C# port preserves the queue/worker shape by using a channel and a background
// worker, while using ASP.NET Core for the HTTP endpoint.

namespace Cozmo.Samples.Examples.IfThisThenThat;

/// <summary>Queue IFTTT sports requests and process them on a worker.</summary>
public static class IftttSportsFlask
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IfThisThenThatServer.RunAsync(
            context,
            "/iftttSports",
            string.Empty,
            async (json, robot) =>
            {
                string alertBody = json.GetProperty("AlertBody").GetString() ??
                    throw new InvalidOperationException("AlertBody is missing.");
                await IfThisThenThatRobot.RunReactionAsync(
                    robot,
                    alertBody,
                    "ifttt_sports.png");
            },
            queueRequests: true);
    }
}
