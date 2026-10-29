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

using System.Collections.Concurrent;
using Anki.Cozmo;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;

namespace Cozmo.Tests;

public sealed class ActionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task SendSingleActionSendsQueueMessageAndStartsAction()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        Task<EvtActionStarted> started = action.WaitFor<EvtActionStarted>(timeout: TestTimeout);

        await harness.Dispatcher.SendSingleActionAsync(action);

        EvtActionStarted evt = await started;
        Assert.Same(action, evt.Action);
        Assert.True(action.IsRunning);
        Assert.NotNull(action.Id);
        Assert.Same(harness.Dispatcher, action.Owner);

        MessageGameToEngine sent = SingleSent(harness.Transport);
        Assert.Equal(MessageGameToEngine.Tag.QueueSingleAction, sent.tag);
        QueueSingleAction queue = sent.QueueSingleAction;
        Assert.Equal(action.Id!.Value, queue.idTag);
        Assert.Equal(0, queue.numRetries);
        Assert.Equal((byte)QueueActionPosition.NOW, queue.position);
        Assert.Equal(RobotActionUnion.Tag.driveOffChargerContacts, queue.action.tag);
    }

    [Fact]
    public async Task ParallelActionUsesInParallelPosition()
    {
        await using Harness harness = await Harness.CreateAsync();

        await harness.Dispatcher.SendSingleActionAsync(new TestAction(), inParallel: true);

        QueueSingleAction queue = SingleSent(harness.Transport).QueueSingleAction;
        Assert.Equal((byte)QueueActionPosition.IN_PARALLEL, queue.position);
    }

    [Fact]
    public async Task SecondNonParallelActionThrowsRobotBusy()
    {
        await using Harness harness = await Harness.CreateAsync();
        await harness.Dispatcher.SendSingleActionAsync(new TestAction());

        await Assert.ThrowsAsync<RobotBusyException>(
            () => harness.Dispatcher.SendSingleActionAsync(new TestAction()));
    }

    [Fact]
    public async Task ParallelActionsCoexist()
    {
        await using Harness harness = await Harness.CreateAsync();
        var first = new TestAction();
        var second = new TestAction();

        await harness.Dispatcher.SendSingleActionAsync(first);
        await harness.Dispatcher.SendSingleActionAsync(second, inParallel: true);

        Assert.Equal(2, harness.Dispatcher.InProgressActions.Count);
        Assert.True(harness.Dispatcher.HasInProgressActions);
    }

    [Fact]
    public async Task SuccessCompletionMarksActionSucceeded()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);
        Task<EvtActionCompleted> waiting = action.WaitForCompletedAsync(TestTimeout);

        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(action, ActionResult.SUCCESS));

        EvtActionCompleted evt = await waiting;
        Assert.True(action.HasSucceeded);
        Assert.Equal(ActionState.Succeeded, evt.State);
        Assert.Null(evt.FailureCode);
        Assert.Equal(ActionResults.Success, action.Result);
        Assert.Empty(harness.Dispatcher.InProgressActions);
    }

    [Theory]
    [InlineData((uint)ActionResult.RUNNING, "running")]
    [InlineData((uint)ActionResult.NOT_STARTED, "not_started")]
    [InlineData((uint)ActionResult.TIMEOUT, "timeout")]
    [InlineData((uint)ActionResult.TRACKS_LOCKED, "tracks_locked")]
    [InlineData((uint)ActionResult.BAD_TAG, "bad_tag")]
    [InlineData((uint)ActionResult.CANCELLED_WHILE_RUNNING, "cancelled")]
    [InlineData((uint)ActionResult.INTERRUPTED, "interrupted")]
    [InlineData((uint)ActionResult.ANIM_ABORTED, "aborted")]
    [InlineData((uint)ActionResult.PATH_PLANNING_FAILED_RETRY, "retry")]
    public async Task FailureResultsMapToCodes(uint result, string expectedCode)
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);

        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(action, (ActionResult)result));

        Assert.True(action.HasFailed);
        Assert.Equal(expectedCode, action.FailureCode);
        Assert.Equal(ActionState.Failed, action.CompletedEvent!.State);
    }

    [Fact]
    public async Task AbortRunningActionMovesToAbortingAndSendsCancel()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);

        await action.AbortAsync();

        Assert.True(action.IsAborting);
        Assert.Single(harness.Dispatcher.AbortingActions);
        Assert.Empty(harness.Dispatcher.InProgressActions);

        MessageGameToEngine[] sent = harness.Transport.SentFrames.Select(Decode).ToArray();
        Assert.Equal(2, sent.Length);
        Assert.Equal(MessageGameToEngine.Tag.CancelActionByIdTag, sent[1].tag);
        Assert.Equal(action.Id!.Value, sent[1].CancelActionByIdTag.idTag);
    }

    [Fact]
    public async Task AbortCompletedActionIsSilentAndSendsNothing()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);
        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(action, ActionResult.SUCCESS));

        await action.AbortAsync(logAbortMessages: true);

        Assert.True(action.HasSucceeded);
        Assert.Single(harness.Transport.SentFrames);
    }

    [Fact]
    public async Task AbortIdleActionThrows()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Dispatcher.AbortActionAsync(action));
        await Assert.ThrowsAsync<InvalidOperationException>(() => action.AbortAsync());
    }

    [Fact]
    public async Task AbortAllActionsSendsWildcardCancel()
    {
        await using Harness harness = await Harness.CreateAsync();
        await harness.Dispatcher.SendSingleActionAsync(new TestAction());
        await harness.Dispatcher.SendSingleActionAsync(new TestAction(), inParallel: true);

        await harness.Dispatcher.AbortAllActionsAsync();

        Assert.Empty(harness.Dispatcher.InProgressActions);
        Assert.Equal(2, harness.Dispatcher.AbortingActions.Count);
        MessageGameToEngine last = Decode(harness.Transport.SentFrames[^1]);
        Assert.Equal(MessageGameToEngine.Tag.CancelAction, last.tag);
        Assert.Equal((int)RobotActionType.UNKNOWN, last.CancelAction.actionType);
    }

    [Fact]
    public async Task AbortedActionCompletedMessageFailsIt()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);
        await action.AbortAsync();

        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(action, ActionResult.CANCELLED_WHILE_RUNNING));

        Assert.True(action.HasFailed);
        Assert.Equal("cancelled", action.FailureCode);
        Assert.Empty(harness.Dispatcher.AbortingActions);
    }

    [Fact]
    public async Task WaitForAllActionsCompletedWaitsForEveryAction()
    {
        await using Harness harness = await Harness.CreateAsync();
        var first = new TestAction();
        var second = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(first);
        await harness.Dispatcher.SendSingleActionAsync(second, inParallel: true);

        Task all = harness.Dispatcher.WaitForAllActionsCompletedAsync();
        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(first, ActionResult.SUCCESS));
        Assert.False(all.IsCompleted);

        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(second, ActionResult.SUCCESS));
        await all.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task WaitForCompletedReturnsImmediatelyWhenAlreadyCompleted()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);
        await harness.Dispatcher.HandleCompletedMessageAsync(Completed(action, ActionResult.SUCCESS));

        EvtActionCompleted evt = await action.WaitForCompletedAsync(TestTimeout);

        Assert.Same(action.CompletedEvent, evt);
    }

    [Fact]
    public async Task ConnectionLossFailsOutstandingActions()
    {
        await using Harness harness = await Harness.CreateAsync();
        var action = new TestAction();
        await harness.Dispatcher.SendSingleActionAsync(action);
        Task<EvtActionCompleted> waiting = action.WaitForCompletedAsync(TestTimeout);

        await harness.Connection.AbortAsync(new ConnectionAbortedException("lost"));

        EvtActionCompleted evt = await waiting;
        Assert.Equal(ActionState.Failed, evt.State);
        Assert.Equal("connection_lost", evt.FailureCode);
        Assert.False(harness.Dispatcher.HasInProgressActions);
    }

    [Fact]
    public void ActionIdsWrapInsideSdkRange()
    {
        ActionIdSource.Reset(ActionIdSource.Last);
        try
        {
            Assert.Equal(ActionIdSource.Last, ActionIdSource.Next());
            Assert.Equal(ActionIdSource.First, ActionIdSource.Next());
        }
        finally
        {
            ActionIdSource.Reset(ActionIdSource.First);
        }
    }

    [Fact]
    public void ActionIdsAreUniqueUnderConcurrency()
    {
        ActionIdSource.Reset(ActionIdSource.First);
        try
        {
            var ids = new ConcurrentBag<uint>();
            Parallel.For(0, 1000, _ => ids.Add(ActionIdSource.Next()));

            Assert.Equal(1000, ids.Distinct().Count());
            Assert.All(ids, id => Assert.InRange(id, ActionIdSource.First, ActionIdSource.Last));
        }
        finally
        {
            ActionIdSource.Reset(ActionIdSource.First);
        }
    }

    [Fact]
    public async Task UnknownSdkActionCompletionIsIgnored()
    {
        await using Harness harness = await Harness.CreateAsync();

        await harness.Dispatcher.HandleCompletedMessageAsync(new RobotCompletedAction
        {
            idTag = ActionIdSource.First + 5,
            result = (uint)ActionResult.SUCCESS,
        });

        Assert.Empty(harness.Dispatcher.InProgressActions);
        Assert.Empty(harness.Dispatcher.AbortingActions);
    }

    private static RobotCompletedAction Completed(TestAction action, ActionResult result) => new()
    {
        idTag = action.Id ?? throw new InvalidOperationException("The action has no id."),
        result = (uint)result,
    };

    private static MessageGameToEngine SingleSent(FakeTransport transport)
    {
        Assert.Single(transport.SentFrames);
        return Decode(transport.SentFrames[0]);
    }

    private static MessageGameToEngine Decode(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
    }

    private sealed class TestAction : CozmoAction
    {
        public override void Encode(RobotActionUnion action)
            => action.driveOffChargerContacts = new Anki.Cozmo.ExternalInterface.DriveOffChargerContacts();
    }

    private sealed class Harness : IAsyncDisposable
    {
        private Harness()
        {
            Transport = new FakeTransport();
            Connection = new CozmoConnection(Transport);
            Dispatcher = new ActionDispatcher(Connection);
        }

        public FakeTransport Transport { get; }

        public CozmoConnection Connection { get; }

        public ActionDispatcher Dispatcher { get; }

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();
            await harness.Connection.ConnectAsync();
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            Dispatcher.Dispose();
            await Connection.DisposeAsync();
            await Transport.DisposeAsync();
        }
    }
}
