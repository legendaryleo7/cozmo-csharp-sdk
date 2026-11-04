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

namespace Cozmo.Samples.Examples;

/// <summary>Runs a repeating light pattern on a light cube.</summary>
/// <remarks>
/// Python's examples subclass <c>LightCube</c> to add a chaser. The C# <see cref="LightCube"/>
/// is sealed, so the port composes the effect in this helper instead. The cube's normal API
/// and state tracking are unchanged.
/// </remarks>
public sealed class LightChaser : IAsyncDisposable
{
    private readonly LightCube _cube;
    private readonly Func<int, IReadOnlyList<Light>> _frameFactory;
    private CancellationTokenSource? _cancellation;
    private Task? _task;

    /// <summary>Creates a chaser for one cube.</summary>
    /// <param name="cube">Cube whose corners are animated.</param>
    /// <param name="frameFactory">Returns the four corner lights for step 0, 1, 2, ...</param>
    public LightChaser(LightCube cube, Func<int, IReadOnlyList<Light>> frameFactory)
    {
        _cube = cube ?? throw new ArgumentNullException(nameof(cube));
        _frameFactory = frameFactory ?? throw new ArgumentNullException(nameof(frameFactory));
    }

    /// <summary>True while the background pattern is running.</summary>
    public bool IsRunning => _task is { IsCompleted: false };

    /// <summary>Starts the pattern; throws if it is already running.</summary>
    public void Start(TimeSpan interval)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Light chaser already running.");
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be positive.");
        }

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;
        _task = RunAsync(interval, token);
    }

    /// <summary>Stops the pattern and waits for the final frame to be sent.</summary>
    /// <param name="turnOff">True to turn the cube lights off afterwards.</param>
    public async Task StopAsync(bool turnOff = true)
    {
        CancellationTokenSource? cancellation = _cancellation;
        Task? task = _task;
        _cancellation = null;
        _task = null;
        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }

        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the chaser is stopped.
            }
        }

        cancellation?.Dispose();
        if (turnOff)
        {
            await _cube.SetLightsOffAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task RunAsync(TimeSpan interval, CancellationToken token)
    {
        int frameIndex = 0;
        while (!token.IsCancellationRequested)
        {
            IReadOnlyList<Light> frame = _frameFactory(frameIndex++);
            if (frame.Count != 4)
            {
                throw new InvalidOperationException("A light-chaser frame must contain four lights.");
            }

            await _cube.SetLightCornersAsync(frame[0], frame[1], frame[2], frame[3])
                .ConfigureAwait(false);
            await Task.Delay(interval, token).ConfigureAwait(false);
        }
    }
}
