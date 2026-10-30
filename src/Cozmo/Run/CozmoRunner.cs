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

namespace Anki.Cozmo.Sdk;

/// <summary>Connects to a Cozmo device, creates the robot and owns their lifetime.</summary>
/// <remarks>
/// <para>
/// Typical use: <c>await using CozmoRunner runner = await CozmoRunner.ConnectAsync();</c>,
/// then use <see cref="Robot"/>. Disposing the runner releases the robot, connection and
/// connector resources it owns.
/// </para>
/// </remarks>
public sealed class CozmoRunner : IAsyncDisposable
{
    private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultInitializationTimeout = TimeSpan.FromSeconds(30);

    private readonly IDeviceConnector _connector;
    private readonly bool _ownsConnector;
    private bool _disposed;

    private CozmoRunner(
        IDeviceConnector connector,
        bool ownsConnector,
        ConnectedDevice device,
        CozmoConnection connection,
        Robot robot)
    {
        _connector = connector;
        _ownsConnector = ownsConnector;
        Device = device;
        Connection = connection;
        Robot = robot;
    }

    /// <summary>The device metadata reported by the connector.</summary>
    public ConnectedDevice Device { get; }

    /// <summary>The active connection to the engine.</summary>
    public CozmoConnection Connection { get; }

    /// <summary>The initialized robot.</summary>
    public Robot Robot { get; }

    /// <summary>Connects to the first available device and returns an initialized robot.</summary>
    /// <param name="connector">Connector to use; null uses <see cref="FirstAvailableConnector"/>.</param>
    /// <param name="handshakeTimeout">How long to wait for the engine handshake.</param>
    /// <param name="initializationTimeout">How long to wait for the robot to become ready.</param>
    /// <param name="cancellationToken">Cancels discovery, connection and initialization.</param>
    /// <exception cref="CozmoConnectionException">No device could be reached.</exception>
    /// <exception cref="TimeoutException">The handshake or initialization did not complete in time.</exception>
    public static async Task<CozmoRunner> ConnectAsync(
        IDeviceConnector? connector = null,
        TimeSpan? handshakeTimeout = null,
        TimeSpan? initializationTimeout = null,
        CancellationToken cancellationToken = default)
    {
        bool ownsConnector = connector is null;
        connector ??= new FirstAvailableConnector();

        ConnectedDevice device;
        try
        {
            device = await connector.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (ownsConnector)
            {
                await connector.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }

        var connection = new CozmoConnection(device.Transport);
        TimeSpan handshakeWait = handshakeTimeout ?? DefaultHandshakeTimeout;
        Task<EvtConnected> connected = connection.WaitFor<EvtConnected>(timeout: handshakeWait);

        try
        {
            await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await connected.WaitAsync(handshakeWait, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            if (ownsConnector)
            {
                await connector.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }

        var robot = new Robot(connection, robotId: 1, isPrimary: true);
        TimeSpan initializationWait = initializationTimeout ?? DefaultInitializationTimeout;
        try
        {
            Task initialization = robot.InitializationTask ??
                throw new InvalidOperationException("The robot did not start initializing.");
            await initialization.WaitAsync(initializationWait, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            robot.Dispose();
            await connection.DisposeAsync().ConfigureAwait(false);
            if (ownsConnector)
            {
                await connector.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }

        return new CozmoRunner(connector, ownsConnector, device, connection, robot);
    }

    /// <summary>Connects, runs a program against the robot, and disposes the connection.</summary>
    /// <param name="program">The program to run.</param>
    /// <param name="connector">Connector to use; null uses <see cref="FirstAvailableConnector"/>.</param>
    /// <param name="cancellationToken">Cancels the connection and forwarded to the program.</param>
    public static async Task RunProgramAsync(
        Func<Robot, Task> program,
        IDeviceConnector? connector = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        await using CozmoRunner runner = await ConnectAsync(
            connector,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await program(runner.Robot).ConfigureAwait(false);
    }

    /// <summary>Disposes the robot, the connection, and the connector when the runner created it.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Robot.Dispose();
        await Connection.DisposeAsync().ConfigureAwait(false);
        if (_ownsConnector)
        {
            await _connector.DisposeAsync().ConfigureAwait(false);
        }
    }
}
