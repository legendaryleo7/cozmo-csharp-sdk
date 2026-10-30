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

namespace Anki.Cozmo.Sdk;

/// <summary>Client for the usbmux daemon protocol used to reach iOS devices.</summary>
/// <remarks>
/// The control connection is subscribed with a <c>Listen</c> request and receives
/// <c>Attached</c>/<c>Detached</c> notifications. Each device connection opens a second
/// connection to the daemon and sends <c>Connect</c>; once the daemon acknowledges it, that
/// stream carries the application's raw bytes and is handed to a <see cref="StreamTransport"/>.
/// </remarks>
internal sealed class UsbMuxClient : IAsyncDisposable
{
    /// <summary>Client name sent in usbmux requests, matching the Python client.</summary>
    public const string ClientVersionString = "pyusbmux";

    /// <summary>Program name sent in usbmux requests, matching the Python client.</summary>
    public const string ProgramName = "pyusbmux";

    /// <summary>Default time to wait for a device, matching Python's <c>DEFAULT_MAX_WAIT</c>.</summary>
    public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly Func<Stream> _streamFactory;
    private readonly TimeSpan _waitTimeout;
    private readonly Dictionary<long, IReadOnlyDictionary<string, object?>> _attached = new();
    private readonly byte[] _readBuffer = new byte[8192];
    private Stream? _controlStream;
    private UsbMuxMessageReader? _controlReader;
    private bool _disposed;

    /// <summary>Creates a client; call <see cref="ConnectAsync"/> before using it.</summary>
    public UsbMuxClient(Func<Stream> streamFactory, TimeSpan? waitTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        _streamFactory = streamFactory;
        _waitTimeout = waitTimeout ?? DefaultWaitTimeout;
        if (_waitTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(waitTimeout), waitTimeout, "The wait timeout must be positive.");
        }
    }

    /// <summary>Currently attached devices keyed by device id.</summary>
    public IReadOnlyDictionary<long, IReadOnlyDictionary<string, object?>> Attached => _attached;

    /// <summary>Opens the control connection and subscribes to attach/detach notifications.</summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_controlStream is not null)
        {
            throw new InvalidOperationException("The usbmux client is already connected.");
        }

        Stream? stream = null;
        try
        {
            stream = CreateStream();
            _controlStream = stream;
            _controlReader = new UsbMuxMessageReader();
            await SendAsync(stream, BuildListenMessage(), cancellationToken).ConfigureAwait(false);

            while (true)
            {
                Dictionary<string, object?> message = await ReadControlMessageAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(GetString(message, "MessageType"), "Result", StringComparison.Ordinal))
                {
                    continue;
                }

                if (GetInt64(message, "Number") != 0)
                {
                    throw new UsbMuxConnectionFailedException();
                }

                return;
            }
        }
        catch (Exception)
        {
            await CloseControlAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Waits until a device with the supplied serial number is attached.</summary>
    /// <param name="serial">Serial number; matching is case-insensitive.</param>
    /// <param name="timeout">Maximum wait; defaults to the client's wait timeout.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The usbmux device id.</returns>
    /// <exception cref="TimeoutException">No matching device appeared in time.</exception>
    public async Task<long> WaitForSerialAsync(
        string serial,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serial);
        ThrowIfDisposed();
        EnsureControlConnection();

        long? existing = FindSerial(serial);
        if (existing is not null)
        {
            return existing.Value;
        }

        DateTime deadline = GetDeadline(timeout);
        while (true)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("No devices matching serial number found");
            }

            try
            {
                await ReadControlMessageWithTimeoutAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("No devices matching serial number found");
            }

            existing = FindSerial(serial);
            if (existing is not null)
            {
                return existing.Value;
            }
        }
    }

    /// <summary>Connects to the first attached device that accepts the supplied port.</summary>
    /// <param name="port">Port on the device to connect to.</param>
    /// <param name="exclude">Device ids that should not be attempted.</param>
    /// <param name="timeout">Maximum wait for a usable device.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <exception cref="TimeoutException">No usable device became available in time.</exception>
    public async Task<(long DeviceId, IReadOnlyDictionary<string, object?> DeviceInfo, StreamTransport Transport)>
        ConnectToFirstDeviceAsync(
            int port,
            ISet<long>? exclude = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureControlConnection();

        DateTime deadline = GetDeadline(timeout);
        var attempted = new HashSet<long>();
        while (true)
        {
            foreach (long deviceId in _attached.Keys.ToList())
            {
                if (attempted.Contains(deviceId))
                {
                    continue;
                }

                if (exclude is not null && exclude.Contains(deviceId))
                {
                    continue;
                }

                attempted.Add(deviceId);
                try
                {
                    return await ConnectToDeviceAsync(deviceId, port, cancellationToken).ConfigureAwait(false);
                }
                catch (UsbMuxException)
                {
                    // Try the next attached device, matching the Python connector.
                }
            }

            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("No available devices");
            }

            try
            {
                await ReadControlMessageWithTimeoutAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("No available devices");
            }
        }
    }

    /// <summary>Opens a dedicated usbmux connection to one device.</summary>
    /// <param name="deviceId">usbmux device id.</param>
    /// <param name="port">Port on the device to connect to.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<(long DeviceId, IReadOnlyDictionary<string, object?> DeviceInfo, StreamTransport Transport)>
        ConnectToDeviceAsync(long deviceId, int port, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        Stream? stream = CreateStream();
        var reader = new UsbMuxMessageReader();
        try
        {
            await SendAsync(stream, BuildConnectMessage(deviceId, port), cancellationToken).ConfigureAwait(false);

            while (true)
            {
                Dictionary<string, object?> message = await ReadMessageAsync(stream, reader, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(GetString(message, "MessageType"), "Result", StringComparison.Ordinal))
                {
                    continue;
                }

                long status = GetInt64(message, "Number");
                switch (status)
                {
                    case 0:
                        IReadOnlyDictionary<string, object?> info =
                            _attached.TryGetValue(deviceId, out IReadOnlyDictionary<string, object?>? properties)
                                ? properties
                                : EmptyProperties;
                        var transport = new StreamTransport(stream, reader.TakeRemaining());
                        stream = null;
                        return (deviceId, info, transport);

                    case 2:
                        throw new DeviceNotConnectedException(
                            $"Device {deviceId} is not currently connected");

                    case 3:
                        throw new UsbMuxConnectionRefusedException(
                            $"Connection refused to device_id={deviceId} port={port}");

                    default:
                        throw new UsbMuxConnectionFailedException(
                            $"Protocol error connecting to device {deviceId}");
                }
            }
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Queries the daemon for all attached devices.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Stream? stream = CreateStream();
        var reader = new UsbMuxMessageReader();
        try
        {
            await SendAsync(stream, BuildListDevicesMessage(), cancellationToken).ConfigureAwait(false);
            bool resultSeen = false;
            while (true)
            {
                Dictionary<string, object?> message = await ReadMessageAsync(stream, reader, cancellationToken)
                    .ConfigureAwait(false);
                string messageType = GetString(message, "MessageType");

                if (string.Equals(messageType, "Result", StringComparison.Ordinal))
                {
                    if (GetInt64(message, "Number") != 0)
                    {
                        throw new UsbMuxConnectionFailedException();
                    }

                    resultSeen = true;
                    continue;
                }

                if (!string.Equals(messageType, "DeviceList", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!resultSeen)
                {
                    throw new UsbMuxProtocolException("The usbmux daemon sent a device list before a result.");
                }

                if (!message.TryGetValue("DeviceList", out object? raw) || raw is not IEnumerable<object?> entries)
                {
                    throw new UsbMuxProtocolException("The usbmux device list message is malformed.");
                }

                var devices = new List<IReadOnlyDictionary<string, object?>>();
                foreach (object? entry in entries)
                {
                    if (entry is not IReadOnlyDictionary<string, object?> device ||
                        !device.TryGetValue("Properties", out object? properties) ||
                        properties is not IReadOnlyDictionary<string, object?> deviceProperties)
                    {
                        throw new UsbMuxProtocolException("A usbmux device list entry is malformed.");
                    }

                    devices.Add(deviceProperties);
                }

                return devices;
            }
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await CloseControlAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private static Dictionary<string, object?> BuildListenMessage()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageType"] = "Listen",
            ["ClientVersionString"] = ClientVersionString,
            ["ProgName"] = ProgramName,
        };
    }

    private static Dictionary<string, object?> BuildListDevicesMessage()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageType"] = "ListDevices",
            ["ClientVersionString"] = ClientVersionString,
            ["ProgName"] = ProgramName,
        };
    }

    private static Dictionary<string, object?> BuildConnectMessage(long deviceId, int port)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageType"] = "Connect",
            ["ClientVersionString"] = ClientVersionString,
            ["ProgName"] = ProgramName,
            ["DeviceID"] = deviceId,
            ["PortNumber"] = NetworkPort(port),
        };
    }

    private static int NetworkPort(int port)
    {
        return (ushort)IPAddress.HostToNetworkOrder((short)port);
    }

    private static async Task SendAsync(
        Stream stream,
        IReadOnlyDictionary<string, object?> message,
        CancellationToken cancellationToken)
    {
        byte[] frame = UsbMuxProtocol.Encode(message);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, object?>> ReadMessageAsync(
        Stream stream,
        UsbMuxMessageReader reader,
        CancellationToken cancellationToken,
        byte[]? readBuffer = null)
    {
        byte[] buffer = readBuffer ?? new byte[8192];
        while (true)
        {
            if (reader.TryRead(out Dictionary<string, object?>? message))
            {
                return message;
            }

            int read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new UsbMuxConnectionFailedException("The usbmux daemon closed the connection.");
            }

            reader.Append(buffer.AsSpan(0, read));
        }
    }

    private async Task<Dictionary<string, object?>> ReadControlMessageAsync(CancellationToken cancellationToken)
    {
        EnsureControlConnection();
        Dictionary<string, object?> message = await ReadMessageAsync(
                _controlStream!,
                _controlReader!,
                cancellationToken,
                _readBuffer)
            .ConfigureAwait(false);
        HandleNotification(message);
        return message;
    }

    private async Task<Dictionary<string, object?>> ReadControlMessageWithTimeoutAsync(
        TimeSpan remaining,
        CancellationToken cancellationToken)
    {
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(remaining);
        try
        {
            return await ReadControlMessageAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    private void HandleNotification(IReadOnlyDictionary<string, object?> message)
    {
        if (!message.TryGetValue("MessageType", out object? rawType) || rawType is not string messageType)
        {
            return;
        }

        switch (messageType)
        {
            case "Attached":
                IReadOnlyDictionary<string, object?> properties = GetDictionary(message, "Properties");
                long deviceId = GetInt64(properties, "DeviceID");
                _attached[deviceId] = properties;
                break;

            case "Detached":
                _attached.Remove(GetInt64(message, "DeviceID"));
                break;
        }
    }

    private long? FindSerial(string serial)
    {
        foreach ((long deviceId, IReadOnlyDictionary<string, object?> properties) in _attached)
        {
            if (properties.TryGetValue("SerialNumber", out object? raw) &&
                raw is string candidate &&
                string.Equals(candidate, serial, StringComparison.OrdinalIgnoreCase))
            {
                return deviceId;
            }
        }

        return null;
    }

    private static string GetString(IReadOnlyDictionary<string, object?> message, string key)
    {
        if (message.TryGetValue(key, out object? value) && value is string text)
        {
            return text;
        }

        throw new UsbMuxProtocolException($"The usbmux message is missing the '{key}' field.");
    }

    private static long GetInt64(IReadOnlyDictionary<string, object?> message, string key)
    {
        if (!message.TryGetValue(key, out object? value) || value is null)
        {
            throw new UsbMuxProtocolException($"The usbmux message is missing the '{key}' field.");
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new UsbMuxProtocolException($"The usbmux message has an invalid '{key}' field.", ex);
        }
    }

    private static IReadOnlyDictionary<string, object?> GetDictionary(
        IReadOnlyDictionary<string, object?> message,
        string key)
    {
        if (message.TryGetValue(key, out object? value) &&
            value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        throw new UsbMuxProtocolException($"The usbmux message is missing the '{key}' dictionary.");
    }

    private DateTime GetDeadline(TimeSpan? timeout)
    {
        return DateTime.UtcNow + (timeout ?? _waitTimeout);
    }

    private Stream CreateStream()
    {
        return _streamFactory() ??
               throw new UsbMuxConnectionFailedException("The usbmux stream factory returned no stream.");
    }

    private void EnsureControlConnection()
    {
        if (_controlStream is null || _controlReader is null)
        {
            throw new InvalidOperationException("The usbmux client is not connected.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private async Task CloseControlAsync()
    {
        Stream? stream = _controlStream;
        _controlStream = null;
        _controlReader = null;
        if (stream is null)
        {
            return;
        }

        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
        {
            // The daemon connection is already gone.
        }
    }
}
