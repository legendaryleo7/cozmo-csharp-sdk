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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anki.Cozmo.Sdk;

/// <summary>Lifecycle state of a <see cref="Behavior"/>, mirroring Python's <c>BEHAVIOR_*</c> strings.</summary>
public enum BehaviorState
{
    /// <summary>The behavior has not been requested to run.</summary>
    Idle,

    /// <summary>The behavior was requested and is waiting for the engine to start it.</summary>
    Requested,

    /// <summary>The behavior is running on the robot.</summary>
    Running,

    /// <summary>The behavior has stopped and is complete.</summary>
    Stopped,
}

/// <summary>Raised when a behavior is requested to start.</summary>
/// <param name="Behavior">The behavior object.</param>
/// <param name="BehaviorTypeName">Name of the behavior type, equivalent to <c>behavior.BehaviorType.Name</c>.</param>
public sealed record EvtBehaviorRequested(Behavior Behavior, string BehaviorTypeName) : CozmoEvent;

/// <summary>Raised when a behavior starts running on the robot.</summary>
/// <param name="Behavior">The behavior object.</param>
/// <param name="BehaviorTypeName">Name of the behavior type, equivalent to <c>behavior.BehaviorType.Name</c>.</param>
public sealed record EvtBehaviorStarted(Behavior Behavior, string BehaviorTypeName) : CozmoEvent;

/// <summary>Raised when a behavior stops.</summary>
/// <param name="Behavior">The behavior object.</param>
/// <param name="BehaviorTypeName">Name of the behavior type, equivalent to <c>behavior.BehaviorType.Name</c>.</param>
public sealed record EvtBehaviorStopped(Behavior Behavior, string BehaviorTypeName) : CozmoEvent;

/// <summary>Describes a behavior the robot is currently performing.</summary>
/// <remarks>
/// <para>
/// Port of <c>cozmo/behavior.py::Behavior</c>. A behavior is created by
/// <c>Robot.StartBehaviorAsync</c> (WP-11) and stopped explicitly with
/// <see cref="StopAsync"/> before the robot does something else.
/// </para>
/// <para>
/// The robot-facing transitions (<c>RequestAsync</c>, <c>NotifyStartedAsync</c> and
/// <c>NotifyStoppedAsync</c>) are internal, mirroring the Python methods the robot calls;
/// the visible surface is the state flags, the three events and the wait helpers.
/// </para>
/// </remarks>
public sealed class Behavior : Dispatcher
{
    /// <summary>Python's default timeout for <c>wait_for_started</c>, in seconds (behavior.py:158).</summary>
    private static readonly TimeSpan DefaultStartTimeout = TimeSpan.FromSeconds(5);

    private readonly Func<Task>? _stopBehaviorAsync;
    private readonly ILogger<Behavior> _logger;
    private BehaviorState _state = BehaviorState.Idle;

    /// <summary>Creates a behavior in the idle state.</summary>
    /// <param name="behaviorType">An entry of <see cref="BehaviorTypes"/>.</param>
    /// <param name="stopBehaviorAsync">
    /// Callback that asks the robot to stop the current behavior before the local stopped
    /// event is raised; the robot passes its <c>SetNoneBehaviorAsync</c> equivalent.
    /// </param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="behaviorType"/> is null.</exception>
    public Behavior(
        CladEnum<global::Anki.Cozmo.ExecutableBehaviorType> behaviorType,
        Func<Task>? stopBehaviorAsync = null,
        ILogger<Behavior>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);
        BehaviorType = behaviorType;
        _stopBehaviorAsync = stopBehaviorAsync;
        _logger = logger ?? NullLogger<Behavior>.Instance;
    }

    /// <summary>Behavior type this instance tracks.</summary>
    public CladEnum<global::Anki.Cozmo.ExecutableBehaviorType> BehaviorType { get; }

    /// <summary>Name of the behavior type, equivalent to Python's <c>behavior.type.name</c>.</summary>
    public string BehaviorTypeName => BehaviorType.Name;

    /// <summary>Current lifecycle state.</summary>
    public BehaviorState State => _state;

    /// <summary>True while the behavior is requested or running and may run on the robot.</summary>
    public bool IsActive => _state is BehaviorState.Requested or BehaviorState.Running;

    /// <summary>True while the behavior is running on the robot.</summary>
    public bool IsRunning => _state == BehaviorState.Running;

    /// <summary>True once the behavior has stopped.</summary>
    public bool IsCompleted => _state == BehaviorState.Stopped;

    /// <summary>Requests that the robot stop performing the behavior.</summary>
    /// <remarks>Has no effect if the behavior is not presently active.</remarks>
    public async Task StopAsync()
    {
        if (!IsActive)
        {
            return;
        }

        if (_stopBehaviorAsync is not null)
        {
            await _stopBehaviorAsync().ConfigureAwait(false);
        }

        await NotifyStoppedAsync().ConfigureAwait(false);
    }

    /// <summary>Waits for the behavior to start running.</summary>
    /// <param name="timeout">
    /// Maximum time to wait; null uses Python's 5-second default. Pass
    /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// </param>
    /// <exception cref="TimeoutException">The behavior did not start in time.</exception>
    /// <exception cref="SdkShutdownException">The behavior was disposed before it started.</exception>
    public async Task WaitForStartedAsync(TimeSpan? timeout = null)
    {
        if (IsRunning || IsCompleted)
        {
            return;
        }

        await WaitFor<EvtBehaviorStarted>(timeout: timeout ?? DefaultStartTimeout).ConfigureAwait(false);
    }

    /// <summary>Waits for the behavior to complete.</summary>
    /// <param name="timeout">
    /// Maximum time to wait after the behavior starts; null waits indefinitely.
    /// </param>
    /// <exception cref="TimeoutException">The behavior did not start or did not complete in time.</exception>
    /// <exception cref="SdkShutdownException">The behavior was disposed before it completed.</exception>
    public async Task WaitForCompletedAsync(TimeSpan? timeout = null)
    {
        if (IsCompleted)
        {
            return;
        }

        // A behavior cannot complete without starting, and if it does not start within a
        // fraction of a second it probably never will (behavior.py:186-192).
        await WaitForStartedAsync().ConfigureAwait(false);
        await WaitFor<EvtBehaviorStopped>(timeout: timeout ?? Timeout.InfiniteTimeSpan)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name} type=\"{BehaviorTypeName}\" state={_state}";

    /// <summary>Marks the behavior requested and raises <see cref="EvtBehaviorRequested"/>.</summary>
    /// <remarks>
    /// Mirrors Python's <c>is_active=True</c> construction path. <c>Robot.StartBehaviorAsync</c>
    /// (WP-11) calls this after creating the behavior; it is public so callers can model a
    /// robot that already accepted the request.
    /// </remarks>
    public async Task RequestAsync()
    {
        _state = BehaviorState.Requested;
        await DispatchEventAsync(new EvtBehaviorRequested(this, BehaviorTypeName)).ConfigureAwait(false);
    }

    /// <summary>Handles the engine reporting that the behavior started.</summary>
    /// <remarks>Port of <c>behavior.py::Behavior._on_engine_started</c>.</remarks>
    internal async Task NotifyStartedAsync()
    {
        if (_state != BehaviorState.Requested)
        {
            if (IsRunning)
            {
                _logger.LogWarning(
                    "Behavior '{Behavior}' unexpectedly reported started when already running.",
                    BehaviorTypeName);
            }

            return;
        }

        _state = BehaviorState.Running;
        await DispatchEventAsync(new EvtBehaviorStarted(this, BehaviorTypeName)).ConfigureAwait(false);
    }

    /// <summary>Marks an active behavior stopped and raises <see cref="EvtBehaviorStopped"/>.</summary>
    /// <remarks>Port of <c>behavior.py::Behavior._set_stopped</c>.</remarks>
    internal async Task NotifyStoppedAsync()
    {
        if (!IsActive)
        {
            return;
        }

        _state = BehaviorState.Stopped;
        await DispatchEventAsync(new EvtBehaviorStopped(this, BehaviorTypeName)).ConfigureAwait(false);
    }
}
