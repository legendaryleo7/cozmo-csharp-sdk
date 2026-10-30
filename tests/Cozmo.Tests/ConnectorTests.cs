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

using System.Net.Sockets;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

/// <summary>Collection used by tests that mutate process environment variables.</summary>
internal static class ConnectorCollection
{
    public const string Name = "Connector";
}

/// <summary>Declares the serialized connector test collection.</summary>
[CollectionDefinition(ConnectorCollection.Name, DisableParallelization = true)]
public sealed class ConnectorCollectionDefinition
{
}

[Collection(ConnectorCollection.Name)]
public sealed class TcpConnectorTests
{
    [Fact]
    public void ConstructorArgumentWinsOverEnvironment()
    {
        using var tcpPort = new EnvironmentVariableScope(TcpConnector.SdkTcpPortEnvironmentVariable, "1111");
        using var cozmoPort = new EnvironmentVariableScope(DeviceConnector.CozmoPortEnvironmentVariable, "2222");

        var connector = new TcpConnector(tcpPort: 3333, cozmoPort: 4444);

        Assert.Equal(3333, connector.TcpPort);
        Assert.Equal(4444, connector.CozmoPort);
        Assert.True(connector.Enabled);
        Assert.Equal(TcpConnector.ConnectorName, connector.Name);
    }

    [Fact]
    public void EnvironmentSuppliesPortsWhenNoArgumentsAreGiven()
    {
        using var tcpPort = new EnvironmentVariableScope(TcpConnector.SdkTcpPortEnvironmentVariable, "1111");
        using var cozmoPort = new EnvironmentVariableScope(DeviceConnector.CozmoPortEnvironmentVariable, "2222");

        var connector = new TcpConnector();

        Assert.Equal(1111, connector.TcpPort);
        Assert.Equal(2222, connector.CozmoPort);
        Assert.True(connector.Enabled);
    }

    [Fact]
    public async Task InvalidEnvironmentPortIsTreatedAsUnset()
    {
        using var tcpPort = new EnvironmentVariableScope(TcpConnector.SdkTcpPortEnvironmentVariable, "not-a-port");
        using var cozmoPort = new EnvironmentVariableScope(DeviceConnector.CozmoPortEnvironmentVariable, "also-bad");

        var connector = new TcpConnector();

        Assert.Null(connector.TcpPort);
        Assert.False(connector.Enabled);
        Assert.Equal(DeviceConnector.DefaultCozmoPort, connector.CozmoPort);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.ConnectAsync());
    }

    [Fact]
    public async Task ConnectsToLoopbackAndReportsTcpDeviceInfo()
    {
        using TcpListener listener = LoopbackServer.Start(out int port);
        var connector = new TcpConnector(tcpPort: port, cozmoPort: port);

        ConnectedDevice device = await connector.ConnectAsync();
        try
        {
            using TcpClient peer = await LoopbackServer.AcceptAsync(listener);
            Assert.Equal(TcpConnector.ConnectorName, device.ConnectorName);
            Assert.Equal("tcp", device.DeviceInfo["device_type"]);
            Assert.Equal($"127.0.0.1:{port}", device.DeviceInfo["host"]);
            var transport = Assert.IsType<TcpTransport>(device.Transport);
            Assert.True(transport.IsConnected);
        }
        finally
        {
            await device.Transport.DisposeAsync();
        }
    }
}

[Collection(ConnectorCollection.Name)]
public sealed class AndroidConnectorTests
{
    private static FakeProcessRunner AdbRunner(string devicesOutput)
    {
        return new FakeProcessRunner((_, arguments) =>
            arguments.Count == 1 && arguments[0] == "devices"
                ? new ProcessResult(0, devicesOutput, string.Empty)
                : new ProcessResult(0, string.Empty, string.Empty));
    }

    [Fact]
    public async Task ParsesDevicesAndUsesForwardCommands()
    {
        using TcpListener listener = LoopbackServer.Start(out int port);
        const string Devices = "List of devices attached\nSER123\tdevice\nOTHER\tunauthorized\n";
        FakeProcessRunner adb = AdbRunner(Devices);
        var connector = new AndroidConnector(
            adbPath: "/fake/adb",
            serial: "ser123",
            cozmoPort: port,
            processRunner: adb);

        ConnectedDevice device = await connector.ConnectAsync();
        try
        {
            using TcpClient peer = await LoopbackServer.AcceptAsync(listener);
            Assert.Equal("android", device.ConnectorName);
            Assert.Equal("android", device.DeviceInfo["device_type"]);
            Assert.Equal("SER123", device.DeviceInfo["serial"]);

            IReadOnlyList<(string Executable, string[] Arguments)> calls = adb.Calls;
            Assert.Equal(3, calls.Count);
            Assert.Equal("/fake/adb", calls[0].Executable);
            Assert.Equal(new[] { "devices" }, calls[0].Arguments);
            Assert.Equal(
                new[] { "-s", "SER123", "forward", "--remove", $"tcp:{port}" },
                calls[1].Arguments);
            Assert.Equal(
                new[] { "-s", "SER123", "forward", $"tcp:{port}", $"tcp:{port}" },
                calls[2].Arguments);
        }
        finally
        {
            await device.Transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task SkipsSerialsThatAreAlreadyConnected()
    {
        using TcpListener listener = LoopbackServer.Start(out int port);
        const string Devices = "List of devices attached\nAAA\tdevice\nBBB\tdevice\n";
        FakeProcessRunner adb = AdbRunner(Devices);
        var connector = new AndroidConnector(adbPath: "/fake/adb", cozmoPort: port, processRunner: adb);

        ConnectedDevice first = await connector.ConnectAsync();
        ConnectedDevice second = await connector.ConnectAsync();
        try
        {
            Assert.Equal("AAA", first.DeviceInfo["serial"]);
            Assert.Equal("BBB", second.DeviceInfo["serial"]);
            Assert.Equal(
                2,
                adb.Calls.Count(call => call.Arguments.Length == 1 && call.Arguments[0] == "devices"));
            Assert.Equal(
                2,
                adb.Calls.Count(call =>
                    call.Arguments.Contains("forward") && !call.Arguments.Contains("--remove")));
        }
        finally
        {
            await first.Transport.DisposeAsync();
            await second.Transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task RemovesForwardWhenAnAttemptFails()
    {
        using TcpListener listener = LoopbackServer.Start(out int port);
        const string Devices = "List of devices attached\nAAA\tdevice\nBBB\tdevice\n";
        FakeProcessRunner adb = AdbRunner(Devices);
        var connector = new AndroidConnector(
            adbPath: "/fake/adb",
            cozmoPort: port,
            processRunner: adb,
            connectionCheck: (device, _) =>
                device.DeviceInfo["serial"] == "AAA"
                    ? Task.FromException(new InvalidOperationException("rejected"))
                    : Task.CompletedTask);

        ConnectedDevice device = await connector.ConnectAsync();
        try
        {
            Assert.Equal("BBB", device.DeviceInfo["serial"]);
            IReadOnlyList<string[]> forwards = adb.Calls
                .Where(call => call.Arguments.Contains("forward"))
                .Select(call => call.Arguments)
                .ToList();
            Assert.Equal(5, forwards.Count);
            Assert.Equal(new[] { "-s", "AAA", "forward", "--remove", $"tcp:{port}" }, forwards[0]);
            Assert.Equal(new[] { "-s", "AAA", "forward", $"tcp:{port}", $"tcp:{port}" }, forwards[1]);
            Assert.Equal(new[] { "-s", "AAA", "forward", "--remove", $"tcp:{port}" }, forwards[2]);
            Assert.Equal(new[] { "-s", "BBB", "forward", "--remove", $"tcp:{port}" }, forwards[3]);
            Assert.Equal(new[] { "-s", "BBB", "forward", $"tcp:{port}", $"tcp:{port}" }, forwards[4]);
        }
        finally
        {
            await device.Transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task RethrowsVersionMismatchAfterEveryDeviceFails()
    {
        using TcpListener listener = LoopbackServer.Start(out int port);
        const string Devices = "List of devices attached\nAAA\tdevice\nBBB\tdevice\n";
        FakeProcessRunner adb = AdbRunner(Devices);
        var mismatches = new Dictionary<string, SdkVersionMismatchException>(StringComparer.Ordinal);
        var connector = new AndroidConnector(
            adbPath: "/fake/adb",
            cozmoPort: port,
            processRunner: adb,
            connectionCheck: (device, _) =>
            {
                var mismatch = new SdkVersionMismatchException(
                    $"mismatch-{device.DeviceInfo["serial"]}",
                    "1.4.12",
                    "3.6.6",
                    "4.0.0");
                mismatches[device.DeviceInfo["serial"]] = mismatch;
                return Task.FromException(mismatch);
            });

        SdkVersionMismatchException thrown =
            await Assert.ThrowsAsync<SdkVersionMismatchException>(() => connector.ConnectAsync());

        Assert.Same(mismatches["BBB"], thrown);
    }

    [Fact]
    public async Task NoUsableDeviceThrowsConnectionException()
    {
        const string Devices = "List of devices attached\nOFFLINE\toffline\n";
        var connector = new AndroidConnector(
            adbPath: "/fake/adb",
            cozmoPort: 5106,
            processRunner: AdbRunner(Devices));

        var exception = await Assert.ThrowsAsync<CozmoConnectionException>(() => connector.ConnectAsync());

        Assert.Equal("No connected Android devices running Cozmo in SDK mode", exception.Message);
    }

    [Fact]
    public async Task AdbFailureSurfacesAsConnectionException()
    {
        var adb = new FakeProcessRunner((_, _) => new ProcessResult(1, string.Empty, "adb: not found"));
        var connector = new AndroidConnector(adbPath: "/fake/adb", cozmoPort: 5106, processRunner: adb);

        var exception = await Assert.ThrowsAsync<CozmoConnectionException>(() => connector.ConnectAsync());

        Assert.Contains("Failed to execute adb command", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AdbPathPrefersConstructorArgument()
    {
        using var environment = new EnvironmentVariableScope(AndroidConnector.AdbPathEnvironmentVariable, "/env/adb");

        var connector = new AndroidConnector(adbPath: "/constructor/adb");

        Assert.Equal("/constructor/adb", connector.AdbPath);
    }

    [Fact]
    public void AdbPathUsesEnvironmentWhenNoArgumentIsGiven()
    {
        using var environment = new EnvironmentVariableScope(AndroidConnector.AdbPathEnvironmentVariable, "/env/adb");

        var connector = new AndroidConnector();

        Assert.Equal("/env/adb", connector.AdbPath);
    }

    [Fact]
    public void AdbPathUsesPathWhenEnvironmentIsUnset()
    {
        using var environment = new EnvironmentVariableScope(AndroidConnector.AdbPathEnvironmentVariable, null);
        string directory = Path.Combine(Path.GetTempPath(), $"cozmo-adb-{Guid.NewGuid():N}");
        string executable = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        string expected = Path.Combine(directory, executable);
        string? previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(expected, string.Empty);
            Environment.SetEnvironmentVariable(
                "PATH",
                directory + Path.PathSeparator + previousPath);

            var connector = new AndroidConnector();

            Assert.Equal(expected, connector.AdbPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Directory.Delete(directory, recursive: true);
        }
    }
}

[Collection(ConnectorCollection.Name)]
public sealed class FirstAvailableConnectorTests
{
    private static SdkVersionMismatchException Mismatch(string name) =>
        new($"mismatch-{name}", "1.4.12", "3.6.6", "4.0.0");

    [Fact]
    public async Task SkipsDisabledTcpAndReturnsAndroid()
    {
        FakeConnector tcp = FakeConnector.Disabled(TcpConnector.ConnectorName);
        FakeConnector android = FakeConnector.Succeeding(AndroidConnector.ConnectorName);
        FakeConnector ios = FakeConnector.Succeeding(IOSConnector.ConnectorName);
        await using var connector = new FirstAvailableConnector(new IDeviceConnector[] { tcp, android, ios });

        ConnectedDevice device = await connector.ConnectAsync();
        try
        {
            Assert.Equal(AndroidConnector.ConnectorName, device.ConnectorName);
            Assert.Equal(0, tcp.CallCount);
            Assert.Equal(1, android.CallCount);
            Assert.Equal(0, ios.CallCount);
        }
        finally
        {
            await device.Transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task TriesConnectorsInSuppliedOrder()
    {
        var order = new List<string>();
        FakeConnector tcp = new(
            TcpConnector.ConnectorName,
            connect: _ =>
            {
                order.Add(TcpConnector.ConnectorName);
                return Task.FromException<ConnectedDevice>(new CozmoConnectionException("no tcp"));
            });
        FakeConnector android = new(
            AndroidConnector.ConnectorName,
            connect: _ =>
            {
                order.Add(AndroidConnector.ConnectorName);
                return Task.FromException<ConnectedDevice>(new CozmoConnectionException("no android"));
            });
        FakeConnector ios = new(
            IOSConnector.ConnectorName,
            connect: _ =>
            {
                order.Add(IOSConnector.ConnectorName);
                return Task.FromException<ConnectedDevice>(new CozmoConnectionException("no ios"));
            });
        await using var connector = new FirstAvailableConnector(new IDeviceConnector[] { tcp, android, ios });

        await Assert.ThrowsAsync<NoDevicesFoundException>(() => connector.ConnectAsync());

        Assert.Equal(new[] { "tcp", "android", "ios" }, order);
    }

    [Fact]
    public async Task RethrowsTcpVersionMismatchFirst()
    {
        SdkVersionMismatchException tcpFailure = Mismatch("tcp");
        FakeConnector tcp = FakeConnector.Failing(TcpConnector.ConnectorName, tcpFailure);
        FakeConnector android = FakeConnector.Failing(AndroidConnector.ConnectorName, Mismatch("android"));
        FakeConnector ios = FakeConnector.Failing(IOSConnector.ConnectorName, Mismatch("ios"));
        await using var connector = new FirstAvailableConnector(new IDeviceConnector[] { tcp, android, ios });

        SdkVersionMismatchException thrown =
            await Assert.ThrowsAsync<SdkVersionMismatchException>(() => connector.ConnectAsync());

        Assert.Same(tcpFailure, thrown);
    }

    [Fact]
    public async Task RethrowsIosVersionMismatchBeforeAndroid()
    {
        SdkVersionMismatchException iosFailure = Mismatch("ios");
        FakeConnector tcp = FakeConnector.Failing(
            TcpConnector.ConnectorName,
            new CozmoConnectionException("no tcp"));
        FakeConnector android = FakeConnector.Failing(AndroidConnector.ConnectorName, Mismatch("android"));
        FakeConnector ios = FakeConnector.Failing(IOSConnector.ConnectorName, iosFailure);
        await using var connector = new FirstAvailableConnector(new IDeviceConnector[] { tcp, android, ios });

        SdkVersionMismatchException thrown =
            await Assert.ThrowsAsync<SdkVersionMismatchException>(() => connector.ConnectAsync());

        Assert.Same(iosFailure, thrown);
    }

    [Fact]
    public async Task RethrowsAndroidVersionMismatchWhenItIsTheOnlyOne()
    {
        SdkVersionMismatchException androidFailure = Mismatch("android");
        FakeConnector tcp = FakeConnector.Failing(
            TcpConnector.ConnectorName,
            new CozmoConnectionException("no tcp"));
        FakeConnector android = FakeConnector.Failing(AndroidConnector.ConnectorName, androidFailure);
        FakeConnector ios = FakeConnector.Failing(
            IOSConnector.ConnectorName,
            new CozmoConnectionException("no ios"));
        await using var connector = new FirstAvailableConnector(new IDeviceConnector[] { tcp, android, ios });

        SdkVersionMismatchException thrown =
            await Assert.ThrowsAsync<SdkVersionMismatchException>(() => connector.ConnectAsync());

        Assert.Same(androidFailure, thrown);
    }

    [Fact]
    public async Task ThrowsNoDevicesFoundWhenEveryConnectorFails()
    {
        await using var connector = new FirstAvailableConnector(new IDeviceConnector[]
        {
            FakeConnector.Failing(TcpConnector.ConnectorName, new CozmoConnectionException("no tcp")),
            FakeConnector.Failing(AndroidConnector.ConnectorName, new CozmoConnectionException("no android")),
            FakeConnector.Failing(IOSConnector.ConnectorName, new CozmoConnectionException("no ios")),
        });

        var exception = await Assert.ThrowsAsync<NoDevicesFoundException>(() => connector.ConnectAsync());

        Assert.Equal("No devices connected running Cozmo in SDK mode", exception.Message);
    }

    [Fact]
    public async Task DisposeDisposesEverySuppliedConnector()
    {
        FakeConnector tcp = FakeConnector.Disabled(TcpConnector.ConnectorName);
        FakeConnector android = FakeConnector.Disabled(AndroidConnector.ConnectorName);
        FakeConnector ios = FakeConnector.Disabled(IOSConnector.ConnectorName);
        var connector = new FirstAvailableConnector(new IDeviceConnector[] { tcp, android, ios });

        await connector.DisposeAsync();

        Assert.Equal(1, tcp.DisposeCount);
        Assert.Equal(1, android.DisposeCount);
        Assert.Equal(1, ios.DisposeCount);
    }
}
