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

using Anki.Cozmo.Sdk;
using System.Net;
using System.Net.Sockets;

namespace Cozmo.Tests;

/// <summary>Shared timeout for connector tests that wait on loopback or in-memory peers.</summary>
internal static class ConnectorTestDefaults
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
}

/// <summary>Loopback listener helper; never binds to a non-local address.</summary>
internal static class LoopbackServer
{
    public static TcpListener Start(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    public static Task<TcpClient> AcceptAsync(TcpListener listener) =>
        listener.AcceptTcpClientAsync().WaitAsync(ConnectorTestDefaults.Timeout);
}

/// <summary>Records adb invocations and returns scripted process output.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Func<string, IReadOnlyList<string>, ProcessResult> _handler;
    private readonly List<(string Executable, string[] Arguments)> _calls = new();
    private readonly object _gate = new();

    public FakeProcessRunner(Func<string, IReadOnlyList<string>, ProcessResult>? handler = null)
    {
        _handler = handler ?? ((_, _) => new ProcessResult(0, "List of devices attached\n", string.Empty));
    }

    public IReadOnlyList<(string Executable, string[] Arguments)> Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.ToArray();
            }
        }
    }

    public Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _calls.Add((executable, arguments.ToArray()));
        }

        return Task.FromResult(_handler(executable, arguments));
    }
}

/// <summary>Connector fake with scripted success or failure and call counting.</summary>
internal sealed class FakeConnector : IDeviceConnector
{
    private readonly Func<CancellationToken, Task<ConnectedDevice>> _connect;

    public FakeConnector(
        string name,
        bool enabled = true,
        Func<CancellationToken, Task<ConnectedDevice>>? connect = null)
    {
        Name = name;
        Enabled = enabled;
        _connect = connect ?? (_ => Task.FromResult(CreateDevice(name)));
    }

    public string Name { get; }

    public bool Enabled { get; }

    public int CallCount { get; private set; }

    public int DisposeCount { get; private set; }

    public Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        return _connect(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    public static FakeConnector Succeeding(string name) => new(name);

    public static FakeConnector Failing(string name, Exception failure) =>
        new(name, connect: _ => Task.FromException<ConnectedDevice>(failure));

    public static FakeConnector Disabled(string name) =>
        new(name, enabled: false, connect: _ => Task.FromException<ConnectedDevice>(
            new InvalidOperationException("A disabled connector must not be called.")));

    public static ConnectedDevice CreateDevice(string connectorName, ICladTransport? transport = null)
    {
        return new ConnectedDevice(
            transport ?? new FakeTransport(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["device_type"] = connectorName,
            },
            connectorName);
    }
}

/// <summary>Sets an environment variable for the duration of a test and restores the previous value.</summary>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previous;

    public EnvironmentVariableScope(string name, string? value)
    {
        _name = name;
        _previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _previous);
    }
}
