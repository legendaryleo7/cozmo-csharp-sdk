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

// Port of examples/if_this_then_that/ifttt_gmail.py.

using System.Text.RegularExpressions;

namespace Cozmo.Samples.Examples.IfThisThenThat;

/// <summary>React when IFTTT reports a new Gmail message.</summary>
public static class IftttGmail
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IfThisThenThatServer.RunAsync(
            context,
            "/iftttGmail",
            "OK",
            async (json, robot) =>
            {
                string fromAddress = json.GetProperty("FromAddress").GetString() ??
                    throw new InvalidOperationException("FromAddress is missing.");
                Match match = Regex.Match(fromAddress, @"([\w.]+)@([\w.]+)");
                string localPart = match.Success ? match.Groups[1].Value : fromAddress;
                await IfThisThenThatRobot.RunReactionAsync(
                    robot,
                    $"Email from {localPart}",
                    "ifttt_gmail.png");
            });
    }
}
