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

/// <summary>Connects directly to the Cozmo app over TCP; intended for testing and debugging.</summary>
/// <remarks>
/// The connector is enabled only when a port is configured, either by the constructor argument
/// or by the <c>SDK_TCP_PORT</c> environment variable. An unparseable or out-of-range value is
/// treated as unset, matching the Python connector.
/// </remarks>
public sealed class TcpConnector : DeviceConnector
{
    /// <summary>Environment variable that supplies the port when no constructor argument is given.</summary>
    public const string SdkTcpPortEnvironmentVariable = "SDK_TCP_PORT";

    /// <summary>Address the app's SDK bridge listens on by default.</summary>
    public const string DefaultIpAddress = "127.0.0.1";

    /// <summary>Connector name reported in <see cref="ConnectedDevice.ConnectorName"/>.</summary>
    public const string ConnectorName = "tcp";

    /// <summary>Creates a TCP connector.</summary>
    /// <param name="tcpPort">Port to connect to; wins over <c>SDK_TCP_PORT</c>.</param>
    /// <param name="ipAddr">Address of the app's SDK bridge.</param>
    /// <param name="cozmoPort">Port of the app's SDK bridge, for parity with the Python connector.</param>
    /// <param name="connectionCheck">Optional check run after the transport opens.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tcpPort"/> is not a valid TCP port.</exception>
    /// <exception cref="ArgumentException"><paramref name="ipAddr"/> is empty.</exception>
    public TcpConnector(
        int? tcpPort = null,
        string ipAddr = DefaultIpAddress,
        int? cozmoPort = null,
        Func<ConnectedDevice, CancellationToken, Task>? connectionCheck = null)
        : base(cozmoPort, connectionCheck)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddr);
        if (tcpPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(tcpPort), tcpPort, "TCP port must be between 1 and 65535.");
        }

        IpAddr = ipAddr;
        TcpPort = tcpPort ?? ConnectorEnvironment.ReadPort(SdkTcpPortEnvironmentVariable);
    }

    /// <summary>Address of the app's SDK bridge.</summary>
    public string IpAddr { get; }

    /// <summary>Configured TCP port, or <c>null</c> when the connector is disabled.</summary>
    public int? TcpPort { get; }

    /// <inheritdoc />
    public override string Name => ConnectorName;

    /// <inheritdoc />
    public override bool Enabled => TcpPort is not null;

    /// <inheritdoc />
    public override async Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default)
    {
        int port = TcpPort ??
                   throw new InvalidOperationException(
                       $"The TCP connector is disabled; set {SdkTcpPortEnvironmentVariable} or pass a port.");

        var transport = new TcpTransport(IpAddr, port);
        try
        {
            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);

            var deviceInfo = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["device_type"] = "tcp",
                ["host"] = $"{IpAddr}:{port}",
            };
            var device = new ConnectedDevice(transport, deviceInfo, Name);
            await RunConnectionCheckAsync(device, cancellationToken).ConfigureAwait(false);
            return device;
        }
        catch (Exception)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
