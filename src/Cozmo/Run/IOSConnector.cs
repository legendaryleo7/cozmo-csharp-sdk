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
using System.Net;
using System.Net.Sockets;

namespace Anki.Cozmo.Sdk;

/// <summary>Connects to an attached iOS device through the usbmux daemon.</summary>
/// <remarks>
/// On Unix the connector talks to <see cref="DefaultUsbMuxSocketPath"/>; on Windows it uses
/// <c>127.0.0.1</c> and <see cref="DefaultUsbMuxPort"/>, matching the Python SDK. Once the
/// usbmux daemon accepts the device connection, the stream carries raw Cozmo CLAD frames.
/// </remarks>
public sealed class IOSConnector : DeviceConnector
{
    /// <summary>Connector name reported in <see cref="ConnectedDevice.ConnectorName"/>.</summary>
    public const string ConnectorName = "ios";

    /// <summary>Default usbmux daemon Unix socket path.</summary>
    public const string DefaultUsbMuxSocketPath = "/var/run/usbmuxd";

    /// <summary>Default usbmux daemon TCP port used on Windows.</summary>
    public const int DefaultUsbMuxPort = 27015;

    /// <summary>Default time to wait for an iOS device, matching Python's <c>DEFAULT_MAX_WAIT</c>.</summary>
    public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(2);

    private readonly Func<Stream> _streamFactory;
    private readonly TimeSpan _waitTimeout;
    private readonly HashSet<long> _connected = new();
    private UsbMuxClient? _usbmux;
    private bool _disposed;

    /// <summary>Creates an iOS connector.</summary>
    /// <param name="serial">Optional device serial number; <c>null</c> selects the first device.</param>
    /// <param name="cozmoPort">Port of the app's SDK bridge.</param>
    /// <param name="streamFactory">
    /// Creates a connected stream to the usbmux daemon. Defaults to the Unix socket or, on
    /// Windows, the loopback TCP port; tests can supply an in-memory duplex stream.
    /// </param>
    /// <param name="waitTimeout">Maximum time to wait for a device; defaults to two seconds.</param>
    /// <param name="usbMuxSocketPath">usbmux Unix socket path used by the default stream factory.</param>
    /// <param name="usbMuxPort">usbmux TCP port used by the default stream factory on Windows.</param>
    /// <param name="connectionCheck">Optional check run after the transport opens.</param>
    /// <exception cref="ArgumentOutOfRangeException">A timeout or port is invalid.</exception>
    /// <exception cref="ArgumentException"><paramref name="usbMuxSocketPath"/> is empty.</exception>
    public IOSConnector(
        string? serial = null,
        int? cozmoPort = null,
        Func<Stream>? streamFactory = null,
        TimeSpan? waitTimeout = null,
        string? usbMuxSocketPath = null,
        int? usbMuxPort = null,
        Func<ConnectedDevice, CancellationToken, Task>? connectionCheck = null)
        : base(cozmoPort, connectionCheck)
    {
        Serial = serial;
        UsbMuxSocketPath = usbMuxSocketPath ?? DefaultUsbMuxSocketPath;
        ArgumentException.ThrowIfNullOrEmpty(UsbMuxSocketPath);

        UsbMuxPort = usbMuxPort ?? DefaultUsbMuxPort;
        if (UsbMuxPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(usbMuxPort), usbMuxPort, "The usbmux port must be between 1 and 65535.");
        }

        WaitTimeout = waitTimeout ?? DefaultWaitTimeout;
        if (WaitTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(waitTimeout), waitTimeout, "The wait timeout must be positive.");
        }

        _waitTimeout = WaitTimeout;
        _streamFactory = streamFactory ?? (() => OpenDefaultUsbMuxStream(UsbMuxSocketPath, UsbMuxPort));
    }

    /// <summary>Optional serial filter; <c>null</c> selects the first usable device.</summary>
    public string? Serial { get; }

    /// <summary>usbmux daemon Unix socket path.</summary>
    public string UsbMuxSocketPath { get; }

    /// <summary>usbmux daemon TCP port used on Windows.</summary>
    public int UsbMuxPort { get; }

    /// <summary>Maximum time to wait for a matching device.</summary>
    public TimeSpan WaitTimeout { get; }

    /// <inheritdoc />
    public override string Name => ConnectorName;

    /// <inheritdoc />
    public override async Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_usbmux is null)
        {
            var created = new UsbMuxClient(_streamFactory, _waitTimeout);
            try
            {
                await created.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await created.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _usbmux = created;
        }

        UsbMuxClient client = _usbmux;
        StreamTransport? transport = null;
        bool returned = false;
        try
        {
            long deviceId;
            IReadOnlyDictionary<string, object?> properties;
            if (Serial is null)
            {
                (deviceId, properties, transport) = await client
                    .ConnectToFirstDeviceAsync(CozmoPort, _connected, _waitTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                deviceId = await client
                    .WaitForSerialAsync(Serial, _waitTimeout, cancellationToken)
                    .ConfigureAwait(false);
                (deviceId, properties, transport) = await client
                    .ConnectToDeviceAsync(deviceId, CozmoPort, cancellationToken)
                    .ConfigureAwait(false);
            }

            string? serialNumber =
                properties.TryGetValue("SerialNumber", out object? rawSerial) && rawSerial is string text
                    ? text
                    : null;
            var deviceInfo = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["device_type"] = "ios",
                ["device_id"] = deviceId.ToString(CultureInfo.InvariantCulture),
                ["serial"] = serialNumber ?? string.Empty,
            };

            var device = new ConnectedDevice(
                transport ?? throw new InvalidOperationException("The usbmux connection returned no transport."),
                deviceInfo,
                Name);
            await RunConnectionCheckAsync(device, cancellationToken).ConfigureAwait(false);

            _connected.Add(deviceId);
            returned = true;
            return device;
        }
        catch (TimeoutException ex)
        {
            await ResetUsbMuxAsync().ConfigureAwait(false);
            throw new CozmoConnectionException(
                "No connected iOS devices running Cozmo in SDK mode",
                ex);
        }
        catch (Exception)
        {
            await ResetUsbMuxAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (!returned && transport is not null)
            {
                await transport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await ResetUsbMuxAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task ResetUsbMuxAsync()
    {
        UsbMuxClient? client = _usbmux;
        _usbmux = null;
        if (client is null)
        {
            return;
        }

        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Releasing the daemon connection must not mask the connection failure.
        }
    }

    private static Stream OpenDefaultUsbMuxStream(string socketPath, int port)
    {
        if (OperatingSystem.IsWindows())
        {
            var client = new TcpClient();
            try
            {
                client.Connect(IPAddress.Loopback, port);
                return client.GetStream();
            }
            catch (Exception)
            {
                client.Dispose();
                throw;
            }
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception)
        {
            socket.Dispose();
            throw;
        }
    }
}
