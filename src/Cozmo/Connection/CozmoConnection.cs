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

using System.Runtime.InteropServices;
using System.Text;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk.Clad;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anki.Cozmo.Sdk;

/// <summary>Raised when the connection completed the handshake with the app.</summary>
public sealed record EvtConnected(CozmoConnection Conn) : CozmoEvent;

/// <summary>Raised when the connection is closed, whether gracefully or after a failure.</summary>
public sealed record EvtConnectionClosed(CozmoConnection Conn, Exception Reason) : CozmoEvent;

/// <summary>Raised for every inbound CLAD message that the connection does not handle itself.</summary>
public sealed record EvtMessageReceived(MessageEngineToGame Message) : CozmoEvent;

/// <summary>CLAD connection to the Cozmo app, including the SDK handshake.</summary>
/// <remarks>
/// <para>
/// The connection owns the transport read loop. Inbound frames are decoded, handshake
/// messages are answered, and everything else is dispatched as
/// <see cref="EvtMessageReceived"/> for higher layers to interpret.
/// </para>
/// <para>
/// Robot creation and <c>WaitForRobot</c> are handled by <c>Robot</c>; this class manages the
/// transport and handshake.
/// </para>
/// </remarks>
public sealed class CozmoConnection : Dispatcher, IAsyncDisposable
{
    /// <summary>Device id the SDK expects from the app's SDK bridge.</summary>
    public const uint SdkDeviceId = 1;

    private readonly ICladTransport _transport;
    private readonly ILogger<CozmoConnection> _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _readLoop;
    private bool _running = true;
    private bool _disposed;

    /// <summary>Creates a connection over the supplied transport.</summary>
    public CozmoConnection(ICladTransport transport, ILogger<CozmoConnection>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _logger = logger ?? NullLogger<CozmoConnection>.Instance;
    }

    /// <summary>True while the underlying transport is connected.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>True once the app completed the SDK handshake.</summary>
    public bool IsUiConnected { get; private set; }

    /// <summary>Connects the transport and starts decoding inbound frames.</summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_readLoop is not null)
        {
            throw new InvalidOperationException("The connection has already been opened.");
        }

        await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        IsConnected = true;
        _readLoop = ReadLoopAsync(_lifetime.Token);
    }

    /// <summary>Packs and sends one CLAD message.</summary>
    public async Task SendAsync(MessageGameToEngine message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var stream = new MemoryStream();
        message.Pack(new CladBinaryWriter(stream));
        await _transport.SendAsync(stream.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes the connection because the SDK is shutting down.</summary>
    public Task ShutdownAsync() => AbortAsync(new SdkShutdownException("The SDK is shutting down."));

    /// <summary>Closes the connection after a failure and fails every pending wait.</summary>
    public async Task AbortAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!_running)
        {
            return;
        }

        _running = false;
        IsConnected = false;

        // Report the close before failing pending waits so a caller waiting for
        // EvtConnectionClosed observes the event rather than the abort exception.
        try
        {
            await DispatchEventAsync(new EvtConnectionClosed(this, exception)).ConfigureAwait(false);
        }
        catch (Exception dispatchFailure)
        {
            _logger.LogWarning(dispatchFailure, "A handler failed while reporting the connection close.");
        }

        AbortPendingWaits(exception);
        StopDispatching();
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _transport.CloseAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_running)
        {
            await ShutdownAsync().ConfigureAwait(false);
        }

        _lifetime.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            await foreach (byte[] frame in _transport.ReceiveFramesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!_running)
                {
                    break;
                }

                try
                {
                    await HandleFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                }
                catch (SdkVersionMismatchException ex)
                {
                    failure = ex;
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failure = new ConnectionAbortedException("Failed to handle a CLAD message.", ex);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during teardown.
        }
        catch (Exception ex)
        {
            failure ??= new ConnectionAbortedException("The connection was lost.", ex);
        }

        if (_running)
        {
            await AbortAsync(failure ?? new ConnectionAbortedException("Lost connection to the device."))
                .ConfigureAwait(false);
        }
    }

    private async Task HandleFrameAsync(byte[] frame, CancellationToken cancellationToken)
    {
        MessageEngineToGame message;
        try
        {
            using var stream = new MemoryStream(frame, writable: false);
            message = MessageEngineToGame.UnpackFrom(new CladBinaryReader(stream));
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            _logger.LogWarning(ex, "Dropping an undecodable CLAD frame of {Length} bytes.", frame.Length);
            return;
        }

        switch (message.tag)
        {
            case MessageEngineToGame.Tag.Ping:
                await HandlePingAsync(message.Ping, cancellationToken).ConfigureAwait(false);
                return;

            case MessageEngineToGame.Tag.UiDeviceConnected:
                await HandleUiDeviceConnectedAsync(message.UiDeviceConnected, cancellationToken).ConfigureAwait(false);
                return;

            default:
                await DispatchEventAsync(new EvtMessageReceived(message)).ConfigureAwait(false);
                return;
        }
    }

    private async Task HandlePingAsync(Ping ping, CancellationToken cancellationToken)
    {
        if (ping.isResponse)
        {
            _logger.LogError("Only the engine should receive ping responses.");
            return;
        }

        var response = new Ping
        {
            counter = ping.counter,
            timeSent_ms = ping.timeSent_ms,
            isResponse = true,
        };
        await SendAsync(new MessageGameToEngine { Ping = response }, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleUiDeviceConnectedAsync(UiDeviceConnected message, CancellationToken cancellationToken)
    {
        if (message.connectionType != (byte)UiConnectionType.SdkOverTcp)
        {
            // This handshake was not addressed to the TCP SDK bridge.
            return;
        }

        if (message.deviceID != SdkDeviceId)
        {
            _logger.LogError("Unexpected device id {DeviceId} in the handshake.", message.deviceID);
            return;
        }

        bool hashesMatch =
            message.toGameCLADHash.AsSpan().SequenceEqual(CladIdentity.EngineToGameHash) &&
            message.toEngineCLADHash.AsSpan().SequenceEqual(CladIdentity.GameToEngineHash);

        bool buildVersionsMatch =
            CladIdentity.BuildVersion == "00000.00000.00000" ||
            string.Equals(CladIdentity.BuildVersion, message.buildVersion, StringComparison.Ordinal);

        if (hashesMatch && !buildVersionsMatch)
        {
            // A hotfix app build may move the version without changing CLAD; accept it when
            // only the major and minor components differ.
            string[] sdkParts = CladIdentity.BuildVersion.Split('.');
            string[] appParts = message.buildVersion.Split('.');
            buildVersionsMatch = sdkParts.Length >= 2 &&
                                 appParts.Length >= 2 &&
                                 string.Equals(sdkParts[0], appParts[0], StringComparison.Ordinal) &&
                                 string.Equals(sdkParts[1], appParts[1], StringComparison.Ordinal);
        }

        if (hashesMatch && buildVersionsMatch)
        {
            var success = new UiDeviceConnectionSuccess
            {
                connectionType = message.connectionType,
                deviceID = message.deviceID,
                buildVersion = CladIdentity.PackageVersion,
                sdkModuleVersion = SdkVersion.Version,
                pythonVersion = RuntimeInformation.FrameworkDescription,
                pythonImplementation = "C#",
                osVersion = RuntimeInformation.OSDescription,
                cpuVersion = RuntimeInformation.ProcessArchitecture.ToString(),
            };
            await SendAsync(new MessageGameToEngine { UiDeviceConnectionSuccess = success }, cancellationToken)
                .ConfigureAwait(false);

            IsUiConnected = true;
            _logger.LogInformation(
                "App connection established. sdk_version={SdkVersion} clad_version={CladVersion} app_build_version={AppVersion}",
                SdkVersion.Version,
                CladIdentity.PackageVersion,
                message.buildVersion);

            await DispatchEventAsync(new EvtConnected(this)).ConfigureAwait(false);

            // Ask for connected and located objects before the animation list; the Python SDK
            // does the same so those responses arrive before the robot is marked ready.
            await SendAsync(new MessageGameToEngine { RequestConnectedObjects = new RequestConnectedObjects() }, cancellationToken)
                .ConfigureAwait(false);
            await SendAsync(new MessageGameToEngine { RequestLocatedObjectStates = new RequestLocatedObjectStates() }, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var wrongVersion = new UiDeviceConnectionWrongVersion
        {
            connectionType = message.connectionType,
            deviceID = message.deviceID,
            buildVersion = CladIdentity.PackageVersion,
        };
        await SendAsync(new MessageGameToEngine { UiDeviceConnectionWrongVersion = wrongVersion }, cancellationToken)
            .ConfigureAwait(false);

        string detail = BuildHandshakeFailureMessage(hashesMatch, buildVersionsMatch, message.buildVersion);
        _logger.LogError("{Detail}", detail);

        throw new SdkVersionMismatchException(
            detail,
            SdkVersion.Version,
            CladIdentity.PackageVersion,
            TrimVersion(message.buildVersion));
    }

    private static string BuildHandshakeFailureMessage(bool hashesMatch, bool buildVersionsMatch, string appBuildVersion)
    {
        const string lineSeparator = "================================================================================";
        var text = new StringBuilder();
        text.Append('\n').Append(lineSeparator).Append('\n');

        if (hashesMatch && !buildVersionsMatch)
        {
            text.Append("App and SDK versions do not match!\n")
                .Append("----------------------------------\n")
                .Append("SDK's cozmoclad version: ").Append(CladIdentity.PackageVersion).Append('\n')
                .Append("         != app version: ").Append(TrimVersion(appBuildVersion)).Append("\n\n");

            if (string.CompareOrdinal(CladIdentity.BuildVersion, appBuildVersion) < 0)
            {
                // The app is newer than the SDK.
                text.Append("Please update your SDK to the newest version.\n")
                    .Append("Download the latest C# SDK and examples from:\n")
                    .Append("http://cozmosdk.anki.bot/docs/downloads.html\n");
            }
            else
            {
                // The SDK is newer than the app.
                text.Append("Please either:\n\n")
                    .Append("1) Update your app to the most recent version on the app store.\n")
                    .Append("2) Or, if you prefer, please determine which SDK version matches\n")
                    .Append("   your app version at: http://go.anki.bot/cozmo-sdk-version\n")
                    .Append("   Then use the C# SDK version listed at that page.\n");
            }
        }
        else
        {
            text.Append("CLAD Hashes do not match!\n")
                .Append("-------------------------\n")
                .Append("Your CLAD versions do not match - connection refused.\n")
                .Append("Please check that you have the most recent versions of both the SDK and the\n")
                .Append("Cozmo app. You may update your SDK by installing the latest Cozmo C# SDK.\n")
                .Append("Please also check the app store for a Cozmo app update.\n");
        }

        text.Append(lineSeparator);
        return text.ToString();
    }

    private static string TrimVersion(string version)
    {
        string[] parts = version.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
            {
                parts[i] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return string.Join('.', parts);
    }
}
