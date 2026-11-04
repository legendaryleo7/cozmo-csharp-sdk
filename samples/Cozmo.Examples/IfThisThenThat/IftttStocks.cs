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

// Port of examples/if_this_then_that/ifttt_stocks.py.

namespace Cozmo.Samples.Examples.IfThisThenThat;

/// <summary>React when IFTTT reports a stock price increase.</summary>
public static class IftttStocks
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IfThisThenThatServer.RunAsync(
            context,
            "/iftttStocks",
            "OK",
            async (json, robot) =>
            {
                string stockName = json.GetProperty("StockName").GetString() ??
                    throw new InvalidOperationException("StockName is missing.");
                string percentage = json.GetProperty("PercentageChange").GetRawText();
                await IfThisThenThatRobot.RunReactionAsync(
                    robot,
                    $"{stockName} is up {percentage} percent",
                    "ifttt_stocks.png");
            });
    }
}
