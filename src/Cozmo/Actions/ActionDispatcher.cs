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

/// <summary>Tracks in-progress actions and relays their completion messages.</summary>
/// <remarks>
/// The dispatcher listens for <see cref="EvtMessageReceived"/> and
/// <see cref="EvtConnectionClosed"/>. If the connection closes, every outstanding action
/// fails instead of remaining pending.
/// </remarks>
public sealed class ActionDispatcher : Dispatcher
{
    private readonly CozmoConnection _connection;
    private readonly ILogger<ActionDispatcher> _logger;
    private readonly Dictionary<uint, CozmoAction> _inProgress = new();
    private readonly Dictionary<uint, CozmoAction> _aborting = new();
    private readonly Handler _messageHandler;
    private readonly Handler _closedHandler;

    /// <summary>Creates a dispatcher bound to a connection.</summary>
    public ActionDispatcher(CozmoConnection connection, ILogger<ActionDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _logger = logger ?? NullLogger<ActionDispatcher>.Instance;
        _messageHandler = connection.AddAsyncHandler<EvtMessageReceived>(OnMessageReceivedAsync);
        _closedHandler = connection.AddAsyncHandler<EvtConnectionClosed>(OnConnectionClosedAsync);
    }

    /// <summary>Actions currently in progress, in insertion order.</summary>
    public IReadOnlyList<CozmoAction> InProgressActions => _inProgress.Values.ToArray();

    /// <summary>Actions that were aborted and are awaiting their completion message.</summary>
    public IReadOnlyList<CozmoAction> AbortingActions => _aborting.Values.ToArray();

    /// <summary>True when at least one action is in progress.</summary>
    public bool HasInProgressActions => _inProgress.Count > 0;

    /// <summary>Sends an action to the robot and marks it running.</summary>
    /// <param name="action">Action to send.</param>
    /// <param name="inParallel">True to allow the action to run alongside others.</param>
    /// <param name="numRetries">How many times the robot may retry the action.</param>
    /// <exception cref="RobotBusyException">A non-parallel action was requested while another is running.</exception>
    /// <exception cref="InvalidOperationException">The action already ran or is running.</exception>
    public async Task SendSingleActionAsync(CozmoAction action, bool inParallel = false, byte numRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(action);

        uint actionId = ActionIdSource.Next();
        action.SetActionId(actionId);
        action.Connection = _connection;
        action.Owner = this;

        if (HasInProgressActions && !inParallel)
        {
            CozmoAction busy = _inProgress.Values.First();
            throw new RobotBusyException(
                $"Robot is already performing {_inProgress.Count} action(s) {busy}.");
        }

        if (action.IsRunning)
        {
            throw new InvalidOperationException("Action is already running.");
        }

        if (action.IsCompleted)
        {
            throw new InvalidOperationException("Action already ran.");
        }

        var queue = new QueueSingleAction
        {
            idTag = actionId,
            numRetries = numRetries,
            position = (byte)(inParallel ? QueueActionPosition.IN_PARALLEL : QueueActionPosition.NOW),
        };
        action.Encode(queue.action);

        await _connection.SendAsync(new MessageGameToEngine { QueueSingleAction = queue }).ConfigureAwait(false);
        _inProgress[actionId] = action;
        await action.StartAsync().ConfigureAwait(false);
    }

    /// <summary>Asks the robot to abort one action.</summary>
    /// <param name="action">Action to abort.</param>
    /// <param name="logAbortMessages">True to log the abort.</param>
    /// <exception cref="InvalidOperationException">The action is idle.</exception>
    public async Task AbortActionAsync(CozmoAction action, bool logAbortMessages = false)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Raises when the action never started, and is a no-op when it already completed.
        action.SetAborting(logAbortMessages);

        if (action.CompletedEventPending)
        {
            _logger.LogDebug("Not sending abort for action {Action} to engine as it just completed.", action);
            return;
        }

        uint actionId = action.Id ?? throw new InvalidOperationException("The action has no id.");
        if (!_inProgress.Remove(actionId))
        {
            // Completion won the race; the action is already out of the in-progress set.
            return;
        }

        _aborting[actionId] = action;
        await _connection.SendAsync(new MessageGameToEngine
        {
            CancelActionByIdTag = new CancelActionByIdTag { idTag = actionId },
        }).ConfigureAwait(false);
    }

    /// <summary>Asks the robot to abort every in-progress action.</summary>
    public async Task AbortAllActionsAsync(bool logAbortMessages = false)
    {
        CozmoAction[] actions = _inProgress.Values.ToArray();
        _inProgress.Clear();
        foreach (CozmoAction action in actions)
        {
            action.SetAborting(logAbortMessages);
            if (action.Id is uint id)
            {
                _aborting[id] = action;
            }
        }

        _logger.LogInformation("Sending abort request for all actions.");
        await _connection.SendAsync(new MessageGameToEngine
        {
            CancelAction = new CancelAction { actionType = (int)RobotActionType.UNKNOWN },
        }).ConfigureAwait(false);
    }

    /// <summary>Waits until every in-progress and aborting action has completed.</summary>
    public async Task WaitForAllActionsCompletedAsync()
    {
        while (true)
        {
            CozmoAction? action = _inProgress.Values.FirstOrDefault() ?? _aborting.Values.FirstOrDefault();
            if (action is null)
            {
                return;
            }

            await action.WaitForCompletedAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Handles one inbound completion message.</summary>
    internal async Task HandleCompletedMessageAsync(RobotCompletedAction message)
    {
        ArgumentNullException.ThrowIfNull(message);

        uint actionId = message.idTag;
        bool isSdkAction = IsSdkActionId(actionId);
        bool wasAborted = false;

        if (!_inProgress.Remove(actionId, out CozmoAction? action))
        {
            if (!_aborting.Remove(actionId, out action))
            {
                if (isSdkAction)
                {
                    _logger.LogError(
                        "Received completed action message for unknown SDK action id {ActionId}.",
                        actionId);
                }

                return;
            }

            wasAborted = true;
        }

        if (!isSdkAction)
        {
            _logger.LogError(
                "Received completed action message for sdk-known {Kind} action id {ActionId} (wasAborted={WasAborted}).",
                ActionIdKind(actionId),
                actionId,
                wasAborted);
        }

        action.CompletedEventPending = true;
        await action.HandleCompletedMessageAsync(message).ConfigureAwait(false);
    }

    /// <summary>Fails every outstanding action, used when the connection is lost.</summary>
    public async Task FailAllActionsAsync(Exception reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        CozmoAction[] actions = _inProgress.Values.Concat(_aborting.Values).ToArray();
        _inProgress.Clear();
        _aborting.Clear();
        foreach (CozmoAction action in actions)
        {
            if (action.IsCompleted)
            {
                continue;
            }

            await action.SetFailedAsync("connection_lost", reason.Message).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _messageHandler.Dispose();
            _closedHandler.Dispose();
            _inProgress.Clear();
            _aborting.Clear();
        }

        base.Dispose(disposing);
    }

    private static bool IsSdkActionId(uint actionId) => actionId >= ActionIdSource.First && actionId <= ActionIdSource.Last;

    private static bool IsGameActionId(uint actionId) =>
        actionId >= (uint)ActionConstants.FIRST_GAME_TAG && actionId <= (uint)ActionConstants.LAST_GAME_TAG;

    private static bool IsEngineActionId(uint actionId) =>
        actionId >= (uint)ActionConstants.FIRST_ENGINE_TAG && actionId <= (uint)ActionConstants.LAST_ENGINE_TAG;

    private static string ActionIdKind(uint actionId)
    {
        if (IsSdkActionId(actionId))
        {
            return "sdk";
        }

        if (IsEngineActionId(actionId))
        {
            return "engine";
        }

        return IsGameActionId(actionId) ? "game" : "unknown";
    }

    private async Task OnMessageReceivedAsync(EvtMessageReceived evt)
    {
        if (evt.Message.tag == MessageEngineToGame.Tag.RobotCompletedAction)
        {
            await HandleCompletedMessageAsync(evt.Message.RobotCompletedAction).ConfigureAwait(false);
        }
    }

    private Task OnConnectionClosedAsync(EvtConnectionClosed evt) => FailAllActionsAsync(evt.Reason);
}
