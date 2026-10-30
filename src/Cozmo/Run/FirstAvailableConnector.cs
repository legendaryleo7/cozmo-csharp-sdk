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

/// <summary>Connects to the first available device, trying TCP, Android and then iOS.</summary>
/// <remarks>
/// Disabled connectors are skipped. When every attempt fails, an
/// <see cref="SdkVersionMismatchException"/> is rethrown using TCP, iOS and Android precedence
/// so the user sees the most relevant compatibility error; otherwise
/// <see cref="NoDevicesFoundException"/> is thrown.
/// </remarks>
public sealed class FirstAvailableConnector : IDeviceConnector
{
    /// <summary>Connector name reported in <see cref="ConnectedDevice.ConnectorName"/>.</summary>
    public const string ConnectorName = "first-available";

    private const string NoDevicesMessage = "No devices connected running Cozmo in SDK mode";

    private readonly IReadOnlyList<IDeviceConnector> _connectors;
    private bool _disposed;

    /// <summary>Creates the connector.</summary>
    /// <param name="connectors">
    /// Connectors to try in order. When omitted, the default TCP, Android and iOS connectors
    /// are created, reading their ports from the environment.
    /// </param>
    /// <param name="connectionCheck">
    /// Optional check applied to the default connectors; ignored when <paramref name="connectors"/>
    /// is supplied because those connectors already own their configuration.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="connectors"/> is empty.</exception>
    public FirstAvailableConnector(
        IEnumerable<IDeviceConnector>? connectors = null,
        Func<ConnectedDevice, CancellationToken, Task>? connectionCheck = null)
    {
        if (connectors is null)
        {
            _connectors = new IDeviceConnector[]
            {
                new TcpConnector(connectionCheck: connectionCheck),
                new AndroidConnector(connectionCheck: connectionCheck),
                new IOSConnector(connectionCheck: connectionCheck),
            };
        }
        else
        {
            var supplied = new List<IDeviceConnector>();
            foreach (IDeviceConnector? connector in connectors)
            {
                supplied.Add(connector ?? throw new ArgumentException(
                    "The connector list cannot contain null entries.",
                    nameof(connectors)));
            }

            if (supplied.Count == 0)
            {
                throw new ArgumentException("At least one connector is required.", nameof(connectors));
            }

            _connectors = supplied;
        }
    }

    /// <inheritdoc />
    public string Name => ConnectorName;

    /// <inheritdoc />
    public bool Enabled => true;

    /// <inheritdoc />
    public async Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var failures = new List<(IDeviceConnector Connector, Exception Failure)>();
        foreach (IDeviceConnector connector in _connectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!connector.Enabled)
            {
                continue;
            }

            try
            {
                return await connector.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add((connector, ex));
            }
        }

        Exception? mismatch = failures
            .Where(failure => failure.Failure is SdkVersionMismatchException)
            .OrderBy(failure => MismatchPrecedence(failure.Connector))
            .Select(failure => failure.Failure)
            .FirstOrDefault();
        if (mismatch is not null)
        {
            throw mismatch;
        }

        throw new NoDevicesFoundException(NoDevicesMessage);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var disposed = new HashSet<IDeviceConnector>();
        foreach (IDeviceConnector connector in _connectors)
        {
            if (!disposed.Add(connector))
            {
                continue;
            }

            await connector.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    private static int MismatchPrecedence(IDeviceConnector connector)
    {
        return connector.Name switch
        {
            var name when string.Equals(name, TcpConnector.ConnectorName, StringComparison.OrdinalIgnoreCase) => 0,
            var name when string.Equals(name, IOSConnector.ConnectorName, StringComparison.OrdinalIgnoreCase) => 1,
            var name when string.Equals(name, AndroidConnector.ConnectorName, StringComparison.OrdinalIgnoreCase) => 2,
            _ => 3,
        };
    }
}
