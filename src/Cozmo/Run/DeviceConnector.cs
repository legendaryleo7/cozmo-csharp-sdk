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

using System.Globalization;

namespace Anki.Cozmo.Sdk;

/// <summary>Base class for connectors that open a transport to the Cozmo app.</summary>
/// <remarks>
/// The Cozmo app listens for the SDK bridge on <see cref="CozmoPort"/>. A constructor argument
/// takes precedence over the <c>COZMO_PORT</c> environment variable, which in turn takes
/// precedence over <see cref="DefaultCozmoPort"/>.
/// </remarks>
public abstract class DeviceConnector : IDeviceConnector
{
    /// <summary>Port the Cozmo app uses for its SDK bridge when nothing overrides it.</summary>
    public const int DefaultCozmoPort = 5106;

    /// <summary>Environment variable that overrides <see cref="DefaultCozmoPort"/>.</summary>
    public const string CozmoPortEnvironmentVariable = "COZMO_PORT";

    private readonly Func<ConnectedDevice, CancellationToken, Task>? _connectionCheck;

    /// <summary>Creates a connector.</summary>
    /// <param name="cozmoPort">
    /// Port of the app's SDK bridge, or <c>null</c> to read <c>COZMO_PORT</c> and then fall back
    /// to <see cref="DefaultCozmoPort"/>.
    /// </param>
    /// <param name="connectionCheck">
    /// Optional callback run after a transport is opened and before the device is returned.
    /// The higher-level runner uses it to perform the SDK handshake so a version mismatch can
    /// be attributed to the device that caused it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cozmoPort"/> is not a valid TCP port.</exception>
    protected DeviceConnector(
        int? cozmoPort = null,
        Func<ConnectedDevice, CancellationToken, Task>? connectionCheck = null)
    {
        if (cozmoPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(cozmoPort), cozmoPort, "The Cozmo port must be between 1 and 65535.");
        }

        CozmoPort = cozmoPort ?? ConnectorEnvironment.ReadPort(CozmoPortEnvironmentVariable) ?? DefaultCozmoPort;
        _connectionCheck = connectionCheck;
    }

    /// <summary>Port of the app's SDK bridge.</summary>
    public int CozmoPort { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public virtual bool Enabled => true;

    /// <inheritdoc />
    public abstract Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Runs the optional post-connect check, if one was supplied to the constructor.</summary>
    /// <param name="device">The newly opened device.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    protected Task RunConnectionCheckAsync(ConnectedDevice device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _connectionCheck is null ? Task.CompletedTask : _connectionCheck(device, cancellationToken);
    }
}

/// <summary>Reads connector settings from the process environment.</summary>
internal static class ConnectorEnvironment
{
    /// <summary>Reads a TCP port, returning null when the variable is missing or invalid.</summary>
    public static int? ReadPort(string variable)
    {
        string? raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw) ||
            !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return null;
        }

        return value is >= 1 and <= 65535 ? value : null;
    }
}
