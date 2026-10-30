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

using System.ComponentModel;
using System.Diagnostics;

namespace Anki.Cozmo.Sdk;

/// <summary>Output of one external process invocation.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Runs an external process and captures its output.</summary>
/// <remarks>
/// Device discovery depends on this seam so tests can exercise adb handling without a real
/// Android SDK; production code uses <see cref="ProcessRunner"/>.
/// </remarks>
public interface IProcessRunner
{
    /// <summary>Runs an executable and waits for it to exit.</summary>
    /// <param name="executable">Executable name or absolute path.</param>
    /// <param name="arguments">Arguments passed without shell interpretation.</param>
    /// <param name="cancellationToken">Cancels the wait, terminating the process.</param>
    /// <returns>The exit code and captured output.</returns>
    Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IProcessRunner"/> backed by <see cref="System.Diagnostics.Process"/>.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>Timeout applied when the constructor does not specify one.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout;

    /// <summary>Creates a process runner.</summary>
    /// <param name="timeout">Maximum run time per invocation; defaults to five seconds.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public ProcessRunner(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? DefaultTimeout;
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The process timeout must be positive.");
        }
    }

    /// <summary>Maximum run time per invocation.</summary>
    public TimeSpan Timeout => _timeout;

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new CozmoConnectionException($"Failed to execute '{executable}': the process did not start.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new CozmoConnectionException($"Failed to execute '{executable}': {ex.Message}", ex);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            // Drain the redirected pipes before leaving so no reader is left pending.
            try
            {
                await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The streams can fail when the process is killed; the primary failure is below.
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new CozmoConnectionException(
                $"Failed to execute '{executable}': timed out after {_timeout.TotalSeconds:0.###} seconds.");
        }

        string output = await standardOutput.ConfigureAwait(false);
        string error = await standardError.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The process may already have exited; there is nothing left to terminate.
        }
    }
}
