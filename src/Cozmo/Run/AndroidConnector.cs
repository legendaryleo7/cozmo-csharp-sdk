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

/// <summary>Connects to an Android device running the Cozmo app in SDK mode over adb.</summary>
/// <remarks>
/// <para>
/// The connector locates adb in this order: the constructor argument, the
/// <c>ANDROID_ADB_PATH</c> environment variable, adb on <c>PATH</c>, and finally the common
/// Android SDK locations checked by the Python SDK on Windows. A missing adb is reported when
/// a connection is attempted so that other connectors can still run.
/// </para>
/// <para>
/// For every attached device it removes any stale forward, adds
/// <c>tcp:&lt;cozmoPort&gt; tcp:&lt;cozmoPort&gt;</c>, and connects to
/// <c>127.0.0.1:&lt;cozmoPort&gt;</c>. The forward is removed again when the attempt fails.
/// </para>
/// </remarks>
public sealed class AndroidConnector : DeviceConnector
{
    /// <summary>Environment variable that supplies the adb executable path.</summary>
    public const string AdbPathEnvironmentVariable = "ANDROID_ADB_PATH";

    /// <summary>Connector name reported in <see cref="ConnectedDevice.ConnectorName"/>.</summary>
    public const string ConnectorName = "android";

    private const string LoopbackAddress = "127.0.0.1";

    private readonly IProcessRunner _processRunner;
    private readonly HashSet<string> _connected = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates an Android connector.</summary>
    /// <param name="adbPath">Path to adb; wins over <c>ANDROID_ADB_PATH</c> and <c>PATH</c>.</param>
    /// <param name="serial">Optional device serial; matching is case-insensitive.</param>
    /// <param name="cozmoPort">Port of the app's SDK bridge.</param>
    /// <param name="processRunner">Process runner used to invoke adb; defaults to <see cref="ProcessRunner"/>.</param>
    /// <param name="connectionCheck">Optional check run after the transport opens.</param>
    public AndroidConnector(
        string? adbPath = null,
        string? serial = null,
        int? cozmoPort = null,
        IProcessRunner? processRunner = null,
        Func<ConnectedDevice, CancellationToken, Task>? connectionCheck = null)
        : base(cozmoPort, connectionCheck)
    {
        AdbPath = ResolveAdbPath(adbPath);
        Serial = serial;
        _processRunner = processRunner ?? new ProcessRunner();
    }

    /// <summary>Resolved adb executable path.</summary>
    public string AdbPath { get; }

    /// <summary>Optional serial filter; matching is case-insensitive.</summary>
    public string? Serial { get; }

    /// <inheritdoc />
    public override string Name => ConnectorName;

    /// <inheritdoc />
    public override async Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default)
    {
        SdkVersionMismatchException? versionMismatch = null;
        IReadOnlyList<string> serials = await GetDevicesAsync(cancellationToken).ConfigureAwait(false);

        foreach (string serial in serials)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_connected.Contains(serial))
            {
                continue;
            }

            if (Serial is not null && !string.Equals(serial, Serial, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                await RemoveForwardAsync(serial, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Python ignores a failed pre-emptive forward removal.
            }

            await AddForwardAsync(serial, cancellationToken).ConfigureAwait(false);

            TcpTransport? transport = null;
            bool connected = false;
            try
            {
                transport = new TcpTransport(LoopbackAddress, CozmoPort);
                await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);

                var deviceInfo = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["device_type"] = "android",
                    ["serial"] = serial,
                };
                var device = new ConnectedDevice(transport, deviceInfo, Name);
                await RunConnectionCheckAsync(device, cancellationToken).ConfigureAwait(false);

                connected = true;
                _connected.Add(serial);
                return device;
            }
            catch (SdkVersionMismatchException ex)
            {
                versionMismatch = ex;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Try the next attached device.
            }
            finally
            {
                if (!connected)
                {
                    await DisposeTransportAsync(transport).ConfigureAwait(false);
                    await SafeRemoveForwardAsync(serial).ConfigureAwait(false);
                }
            }
        }

        if (versionMismatch is not null)
        {
            throw versionMismatch;
        }

        throw new CozmoConnectionException("No connected Android devices running Cozmo in SDK mode");
    }

    private async Task<IReadOnlyList<string>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await ExecAdbAsync(new[] { "devices" }, cancellationToken).ConfigureAwait(false);
        var serials = new List<string>();
        foreach (string line in result.StandardOutput.Split('\n'))
        {
            string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 || !string.Equals(fields[1], "device", StringComparison.Ordinal))
            {
                continue;
            }

            serials.Add(fields[0]);
        }

        return serials;
    }

    private Task AddForwardAsync(string serial, CancellationToken cancellationToken)
    {
        string portSpec = PortSpec;
        return ExecAdbAsync(
            new[] { "-s", serial, "forward", portSpec, portSpec },
            cancellationToken);
    }

    private Task RemoveForwardAsync(string serial, CancellationToken cancellationToken)
    {
        return ExecAdbAsync(
            new[] { "-s", serial, "forward", "--remove", PortSpec },
            cancellationToken);
    }

    private async Task SafeRemoveForwardAsync(string serial)
    {
        try
        {
            await RemoveForwardAsync(serial, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cleanup must not mask the connection failure that triggered it.
        }
    }

    private async Task<ProcessResult> ExecAdbAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(AdbPath, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CozmoConnectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CozmoConnectionException($"Failed to execute adb command {AdbPath}: {ex.Message}", ex);
        }

        if (result.ExitCode != 0)
        {
            string command = string.Join(' ', arguments);
            throw new CozmoConnectionException(
                $"Failed to execute adb command {AdbPath} {command}: {result.StandardError}");
        }

        return result;
    }

    private async Task DisposeTransportAsync(TcpTransport? transport)
    {
        if (transport is null)
        {
            return;
        }

        try
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A failed attempt must not mask the original failure.
        }
    }

    private string PortSpec => string.Create(
        CultureInfo.InvariantCulture,
        $"tcp:{CozmoPort}");

    private static string ResolveAdbPath(string? adbPath)
    {
        if (!string.IsNullOrWhiteSpace(adbPath))
        {
            return adbPath;
        }

        string? environmentPath = Environment.GetEnvironmentVariable(AdbPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return environmentPath;
        }

        string executable = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        string? pathCandidate = FindOnPath(executable);
        if (pathCandidate is not null)
        {
            return pathCandidate;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (string variable in new[] { "LOCALAPPDATA", "ProgramFiles", "ProgramFiles(x86)" })
            {
                string? root = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                string candidate = Path.Combine(root, "Android", "android-sdk", "platform-tools", "adb.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        // The Python connector falls back to the bare command on non-Windows platforms and only
        // reports the missing tools when the command is executed.
        return executable;
    }

    private static string? FindOnPath(string executable)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(directory, executable);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
