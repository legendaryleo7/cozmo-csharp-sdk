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

namespace Cozmo.Samples.Examples.MultiRobot;

/// <summary>Connects one of two robots for the multi-robot examples.</summary>
/// <remarks>
/// The Python examples call <c>cozmo.connect_on_loop</c> twice. The C# runner needs a
/// distinct connector per robot, so this helper selects an Android/iOS serial or a TCP port
/// from the command line.
/// </remarks>
public sealed class MultiRobotConnection : IAsyncDisposable
{
    private readonly IDeviceConnector _connector;

    private MultiRobotConnection(CozmoRunner runner, IDeviceConnector connector)
    {
        Runner = runner;
        _connector = connector;
    }

    /// <summary>The connected runner for this robot.</summary>
    public CozmoRunner Runner { get; }

    /// <summary>Connects robot <paramref name="index"/> (1 or 2) using the command-line options.</summary>
    public static async Task<MultiRobotConnection> ConnectAsync(
        string[] arguments,
        int index,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var options = MultiRobotOptions.Parse(arguments);
        IDeviceConnector connector = options.CreateConnector(index);
        CozmoRunner runner;
        try
        {
            runner = await CozmoRunner.ConnectAsync(
                connector,
                cancellationToken: cancellationToken);
        }
        catch
        {
            await connector.DisposeAsync();
            throw;
        }

        try
        {
            DriveOffChargerContacts action = await runner.Robot.DriveOffChargerContactsAsync();
            await action.WaitForCompletedAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Warning: drive-off-charger did not complete within 10 seconds.");
        }
        catch
        {
            await runner.DisposeAsync();
            await connector.DisposeAsync();
            throw;
        }

        return new MultiRobotConnection(runner, connector);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Runner.DisposeAsync();
        await _connector.DisposeAsync();
    }
}

internal sealed class MultiRobotOptions
{
    private MultiRobotOptions(string connector, string? serial1, string? serial2, int? port1, int? port2)
    {
        Connector = connector;
        Serial1 = serial1;
        Serial2 = serial2;
        Port1 = port1;
        Port2 = port2;
    }

    public string Connector { get; }

    public string? Serial1 { get; }

    public string? Serial2 { get; }

    public int? Port1 { get; }

    public int? Port2 { get; }

    public static MultiRobotOptions Parse(string[] arguments)
    {
        string connector = "android";
        string? serial1 = null;
        string? serial2 = null;
        int? port1 = null;
        int? port2 = null;
        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--connector" when index + 1 < arguments.Length:
                    connector = arguments[++index];
                    break;
                case "--serial1" when index + 1 < arguments.Length:
                    serial1 = arguments[++index];
                    break;
                case "--serial2" when index + 1 < arguments.Length:
                    serial2 = arguments[++index];
                    break;
                case "--port1" when index + 1 < arguments.Length:
                    port1 = int.Parse(arguments[++index], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--port2" when index + 1 < arguments.Length:
                    port2 = int.Parse(arguments[++index], System.Globalization.CultureInfo.InvariantCulture);
                    break;
            }
        }

        return new MultiRobotOptions(connector, serial1, serial2, port1, port2);
    }

    public IDeviceConnector CreateConnector(int index)
    {
        if (index is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Robot index must be 1 or 2.");
        }

        string? serial = index == 1 ? Serial1 : Serial2;
        int? port = index == 1 ? Port1 : Port2;
        return Connector switch
        {
            "android" => new AndroidConnector(serial: serial ?? throw new ArgumentException(
                $"Pass --serial{index} so each robot uses a different Android device.")),
            "ios" => new IOSConnector(serial: serial ?? throw new ArgumentException(
                $"Pass --serial{index} so each robot uses a different iOS device.")),
            "tcp" => new TcpConnector(tcpPort: port ?? throw new ArgumentException(
                $"Pass --port{index} so each robot uses a different TCP bridge.")),
            _ => throw new ArgumentException(
                $"Unknown connector '{Connector}'. Use android, ios or tcp.",
                nameof(Connector)),
        };
    }
}
