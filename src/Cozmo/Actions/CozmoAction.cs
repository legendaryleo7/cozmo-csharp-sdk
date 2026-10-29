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

using Anki.Cozmo.ExternalInterface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anki.Cozmo.Sdk;

/// <summary>Base class for a high-level task the robot can perform.</summary>
/// <remarks>
/// An action is created by a robot command (WP-11), then sent by
/// <see cref="ActionDispatcher.SendSingleActionAsync"/>. Callers can await
/// <see cref="WaitForCompletedAsync"/>, register <see cref="OnCompleted"/>, or
/// <see cref="AbortAsync"/> while it runs.
/// </remarks>
public abstract class CozmoAction : Dispatcher
{
    private readonly bool _enableAbortLogging;
    private uint? _actionId;
    private ActionState _state = ActionState.Idle;
    private string? _failureCode;
    private string? _failureReason;
    private CladEnum<global::Anki.Cozmo.ActionResult>? _result;
    private EvtActionCompleted? _completedEvent;
    private bool _completedEventPending;

    /// <summary>Creates an action.</summary>
    /// <param name="enableAbortLogging">False to silence abort messages for actions aborted by design.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    protected CozmoAction(bool enableAbortLogging = true, ILogger? logger = null)
    {
        _enableAbortLogging = enableAbortLogging;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Connection the action was sent over; set by the action dispatcher.</summary>
    public CozmoConnection? Connection { get; internal set; }

    /// <summary>Dispatcher that sent the action; set by the action dispatcher.</summary>
    public ActionDispatcher? Owner { get; internal set; }

    /// <summary>SDK action id, or null before the action is sent.</summary>
    public uint? Id => _actionId;

    /// <summary>Current lifecycle state.</summary>
    public ActionState State => _state;

    /// <summary>True while the action is in progress.</summary>
    public bool IsRunning => _state == ActionState.Running;

    /// <summary>True once the action has succeeded or failed.</summary>
    public bool IsCompleted => _state is ActionState.Succeeded or ActionState.Failed;

    /// <summary>True while the action is awaiting its completion message after an abort request.</summary>
    public bool IsAborting => _state == ActionState.Aborting;

    /// <summary>True when the action completed successfully.</summary>
    public bool HasSucceeded => _state == ActionState.Succeeded;

    /// <summary>True when the action completed with a failure.</summary>
    public bool HasFailed => _state == ActionState.Failed;

    /// <summary>Short failure code, or null when no failure occurred.</summary>
    public string? FailureCode => _failureCode;

    /// <summary>Human-readable failure reason, or null when no failure occurred.</summary>
    public string? FailureReason => _failureReason;

    /// <summary>CLAD result reported by the robot, or null before completion or for unknown results.</summary>
    public CladEnum<global::Anki.Cozmo.ActionResult>? Result => _result;

    /// <summary>The completion event, once the action has completed.</summary>
    public EvtActionCompleted? CompletedEvent => _completedEvent;

    /// <summary>Logger used for action diagnostics.</summary>
    protected ILogger Logger { get; }

    internal bool CompletedEventPending
    {
        get => _completedEventPending;
        set => _completedEventPending = value;
    }

    /// <summary>Fills the CLAD robot-action union alternative for this action.</summary>
    /// <param name="action">Union pre-created by the action dispatcher.</param>
    public abstract void Encode(RobotActionUnion action);

    /// <summary>Asks the robot to abort the action.</summary>
    /// <param name="logAbortMessages">True to log the abort.</param>
    /// <exception cref="InvalidOperationException">The action is idle or was never dispatched.</exception>
    public async Task AbortAsync(bool logAbortMessages = false)
    {
        ActionDispatcher owner = Owner ?? throw new InvalidOperationException(
            "The action has not been dispatched by an ActionDispatcher.");
        await owner.AbortActionAsync(this, logAbortMessages).ConfigureAwait(false);
    }

    /// <summary>Waits for the action to complete.</summary>
    /// <param name="timeout">
    /// Maximum time to wait; null waits indefinitely, matching the Python
    /// <c>wait_for_completed(timeout=None)</c> default.
    /// </param>
    /// <exception cref="TimeoutException">The action did not complete in time.</exception>
    public Task<EvtActionCompleted> WaitForCompletedAsync(TimeSpan? timeout = null)
    {
        if (IsCompleted)
        {
            return Task.FromResult(_completedEvent ?? throw new InvalidOperationException(
                "The action completed without a completion event."));
        }

        return WaitFor<EvtActionCompleted>(timeout: timeout ?? Timeout.InfiniteTimeSpan);
    }

    /// <summary>Registers a handler called when the action completes.</summary>
    public Handler OnCompleted(Action<EvtActionCompleted> handler) => AddHandler(handler);

    /// <summary>Maps a completion message and dispatches the matching completion event.</summary>
    /// <param name="message">Completion message from the robot.</param>
    public async Task HandleCompletedMessageAsync(RobotCompletedAction message)
    {
        ArgumentNullException.ThrowIfNull(message);
        await OnCompletedMessageAsync(message).ConfigureAwait(false);
    }

    /// <summary>Hook for subclasses that extract action-specific result data.</summary>
    protected virtual Task OnCompletedMessageAsync(RobotCompletedAction message) => MapResultAsync(message);

    /// <summary>Extensible factory for the success completion event.</summary>
    protected virtual EvtActionCompleted CreateCompletedEvent() => new(this, _state, null, null);

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name} state={_state}";

    internal void SetActionId(uint actionId) => _actionId = actionId;

    internal async Task StartAsync()
    {
        _state = ActionState.Running;
        await DispatchEventAsync(new EvtActionStarted(this)).ConfigureAwait(false);
    }

    internal async Task SetCompletedAsync(RobotCompletedAction message)
    {
        _ = message;
        _state = ActionState.Succeeded;
        _completedEventPending = false;
        _completedEvent = CreateCompletedEvent();
        await DispatchEventAsync(_completedEvent).ConfigureAwait(false);
    }

    internal async Task SetFailedAsync(string code, string reason)
    {
        _state = ActionState.Failed;
        _failureCode = code;
        _failureReason = reason;
        _completedEventPending = false;
        _completedEvent = new EvtActionCompleted(this, _state, code, reason);
        await DispatchEventAsync(_completedEvent).ConfigureAwait(false);
    }

    internal void SetAborting(bool logAbortMessages)
    {
        if (!IsRunning)
        {
            if (IsCompleted)
            {
                if (_enableAbortLogging && logAbortMessages)
                {
                    Logger.LogInformation(
                        "Cannot abort action {Action} - already completed with state {State}.",
                        this,
                        _state);
                }

                return;
            }

            throw new InvalidOperationException("Action isn't currently running.");
        }

        if (_enableAbortLogging && logAbortMessages)
        {
            Logger.LogInformation("Aborting action {Action}.", this);
        }

        _state = ActionState.Aborting;
    }

    private async Task MapResultAsync(RobotCompletedAction message)
    {
        uint resultId = message.result;
        _result = CladEnum<global::Anki.Cozmo.ActionResult>.TryFromId(resultId, out var entry) ? entry : null;
        if (_result is null)
        {
            Logger.LogError("ActionResults has no entry for result id {ResultId}.", resultId);
        }

        switch (resultId)
        {
            case (uint)global::Anki.Cozmo.ActionResult.SUCCESS:
                await SetCompletedAsync(message).ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.RUNNING:
                await SetFailedAsync("running", "Action was still running").ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.NOT_STARTED:
                await SetFailedAsync("not_started", "Action was not started").ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.TIMEOUT:
                await SetFailedAsync("timeout", "Action timed out").ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.TRACKS_LOCKED:
                await SetFailedAsync("tracks_locked", "Action failed due to tracks locked").ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.BAD_TAG:
                await SetFailedAsync("bad_tag", "Action failed due to bad tag").ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.CANCELLED_WHILE_RUNNING:
                await SetFailedAsync("cancelled", "Action was cancelled while running").ConfigureAwait(false);
                return;

            case (uint)global::Anki.Cozmo.ActionResult.INTERRUPTED:
                await SetFailedAsync("interrupted", "Action was interrupted").ConfigureAwait(false);
                return;
        }

        uint category = resultId >> (int)global::Anki.Cozmo.ARCBitShift.NUM_BITS;
        if (category == (uint)global::Anki.Cozmo.ActionResultCategory.ABORT)
        {
            await SetFailedAsync("aborted", "Action failed").ConfigureAwait(false);
        }
        else if (category == (uint)global::Anki.Cozmo.ActionResultCategory.RETRY)
        {
            await SetFailedAsync("retry", "Action failed but can be retried").ConfigureAwait(false);
        }
        else
        {
            await SetFailedAsync("unknown", "Action failed with unknown reason").ConfigureAwait(false);
            Logger.LogError("Received unknown action result status {ResultId}.", resultId);
        }
    }
}
