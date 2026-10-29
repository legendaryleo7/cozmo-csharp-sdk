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

namespace Cozmo.Tests;

public sealed class DispatcherTests
{
    [Fact]
    public async Task DispatchCallsRegisteredHandler()
    {
        using var dispatcher = new Dispatcher();
        EvtTestBase? received = null;
        using Handler handler = dispatcher.AddHandler<EvtTestBase>(evt => received = evt);

        await dispatcher.DispatchEventAsync(new EvtTestBase(7));

        Assert.NotNull(received);
        Assert.Equal(7, received.Value);
    }

    [Fact]
    public async Task HandlersRunChildThenParent()
    {
        using var parent = new Dispatcher();
        using var child = new Dispatcher();
        child.SetParentDispatcher(parent);
        var order = new List<string>();

        using Handler childHandler = child.AddHandler<EvtTestBase>(_ => order.Add("child-handler"));
        using Handler parentHandler = parent.AddHandler<EvtTestBase>(_ => order.Add("parent-handler"));

        await child.DispatchEventAsync(new EvtTestBase(1));

        Assert.Equal(new[] { "child-handler", "parent-handler" }, order);
    }

    [Fact]
    public async Task ChildDispatcherReceivesParentEvents()
    {
        using var parent = new Dispatcher();
        using var child = new Dispatcher();
        parent.AddChildDispatcher(child);
        int childCount = 0;
        using Handler handler = child.AddHandler<EvtTestBase>(_ => childCount++);

        await parent.DispatchEventAsync(new EvtTestBase(1));

        Assert.Equal(1, childCount);
    }

    [Fact]
    public async Task HandlerForBaseEventReceivesDerivedEvent()
    {
        using var dispatcher = new Dispatcher();
        int count = 0;
        using Handler handler = dispatcher.AddHandler<EvtTestBase>(_ => count++);

        await dispatcher.DispatchEventAsync(new EvtTestChild(1, "text"));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task OneShotHandlerFiresOnce()
    {
        using var dispatcher = new Dispatcher();
        int count = 0;
        using Handler handler = dispatcher.AddOneShotHandler<EvtTestBase>(_ => count++);

        await dispatcher.DispatchEventAsync(new EvtTestBase(1));
        await dispatcher.DispatchEventAsync(new EvtTestBase(2));

        Assert.Equal(1, count);
        Assert.False(handler.IsEnabled);
    }

    [Fact]
    public async Task FilteredOneShotStaysArmedUntilItMatches()
    {
        using var dispatcher = new Dispatcher();
        int count = 0;
        var filter = Filter.For<EvtTestBase>().Where(evt => evt.Value == 2);
        using Handler handler = dispatcher.AddOneShotHandler<EvtTestBase>(_ => count++, filter);

        await dispatcher.DispatchEventAsync(new EvtTestBase(1));
        Assert.Equal(0, count);

        await dispatcher.DispatchEventAsync(new EvtTestBase(2));
        await dispatcher.DispatchEventAsync(new EvtTestBase(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task WaitForReturnsMatchingEvent()
    {
        using var dispatcher = new Dispatcher();
        Task<EvtTestBase> waiting = dispatcher.WaitFor<EvtTestBase>(evt => evt.Value == 5, TimeSpan.FromSeconds(2));

        await dispatcher.DispatchEventAsync(new EvtTestBase(4));
        Assert.False(waiting.IsCompleted);

        await dispatcher.DispatchEventAsync(new EvtTestBase(5));

        EvtTestBase result = await waiting;
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task WaitForAcceptsFilter()
    {
        using var dispatcher = new Dispatcher();
        Task<CozmoEvent> waiting = dispatcher.WaitFor(
            Filter.For<EvtTestBase>().Where(evt => evt.Value == 3),
            TimeSpan.FromSeconds(2));

        await dispatcher.DispatchEventAsync(new EvtTestBase(3));

        CozmoEvent result = await waiting;
        Assert.Equal(3, Assert.IsType<EvtTestBase>(result).Value);
    }

    [Fact]
    public async Task WaitForTimesOut()
    {
        using var dispatcher = new Dispatcher();

        await Assert.ThrowsAsync<TimeoutException>(
            () => dispatcher.WaitFor<EvtTestBase>(timeout: TimeSpan.FromMilliseconds(25)));
    }

    [Fact]
    public async Task RemoveHandlerStopsDelivery()
    {
        using var dispatcher = new Dispatcher();
        int count = 0;
        void OnEvent(EvtTestBase evt) => count++;

        dispatcher.AddHandler<EvtTestBase>(OnEvent);
        await dispatcher.DispatchEventAsync(new EvtTestBase(1));
        dispatcher.RemoveHandler<EvtTestBase>(OnEvent);
        await dispatcher.DispatchEventAsync(new EvtTestBase(2));

        Assert.Equal(1, count);
        Assert.Throws<InvalidOperationException>(() => dispatcher.RemoveHandler<EvtTestBase>(OnEvent));
    }

    [Fact]
    public async Task StopPropagationHaltsRemainingHandlersAndParents()
    {
        using var parent = new Dispatcher();
        using var child = new Dispatcher();
        child.SetParentDispatcher(parent);
        bool secondCalled = false;
        bool parentCalled = false;

        using Handler stop = child.AddHandler<EvtTestBase>(_ => throw new StopPropagationException());
        using Handler second = child.AddHandler<EvtTestBase>(_ => secondCalled = true);
        using Handler parentHandler = parent.AddHandler<EvtTestBase>(_ => parentCalled = true);

        await child.DispatchEventAsync(new EvtTestBase(1));

        Assert.False(secondCalled);
        Assert.False(parentCalled);
    }

    [Fact]
    public async Task CyclicParentChildWiringDeliversOnce()
    {
        using var first = new Dispatcher();
        using var second = new Dispatcher();
        first.AddChildDispatcher(second);
        second.SetParentDispatcher(first);
        int count = 0;
        using Handler handler = second.AddHandler<EvtTestBase>(_ => count++);

        await first.DispatchEventAsync(new EvtTestBase(1));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StoppedDispatcherDoesNotDispatchOrAcceptHandlers()
    {
        using var dispatcher = new Dispatcher();
        int count = 0;
        using Handler handler = dispatcher.AddHandler<EvtTestBase>(_ => count++);
        dispatcher.StopDispatching();

        await dispatcher.DispatchEventAsync(new EvtTestBase(1));
        using Handler late = dispatcher.AddHandler<EvtTestBase>(_ => count++);

        Assert.Equal(0, count);
        Assert.False(late.IsEnabled);
        Assert.True(handler.IsEnabled);
    }

    [Fact]
    public async Task WaitForAfterStopThrowsSdkShutdown()
    {
        using var dispatcher = new Dispatcher();
        dispatcher.StopDispatching();

        await Assert.ThrowsAsync<SdkShutdownException>(
            () => dispatcher.WaitFor<EvtTestBase>(timeout: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task AbortPendingWaitsFailsWaiters()
    {
        using var dispatcher = new Dispatcher();
        Task<EvtTestBase> waiting = dispatcher.WaitFor<EvtTestBase>(timeout: TimeSpan.FromSeconds(5));

        dispatcher.AbortPendingWaits(new SdkShutdownException("connection closed"));

        var exception = await Assert.ThrowsAsync<SdkShutdownException>(() => waiting);
        Assert.Contains("connection closed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncHandlerIsAwaited()
    {
        using var dispatcher = new Dispatcher();
        bool completed = false;
        using Handler handler = dispatcher.AddAsyncHandler<EvtTestBase>(async _ =>
        {
            await Task.Yield();
            completed = true;
        });

        await dispatcher.DispatchEventAsync(new EvtTestBase(1));

        Assert.True(completed);
    }

    [Fact]
    public async Task HandlerDisposeStopsDelivery()
    {
        using var dispatcher = new Dispatcher();
        int count = 0;
        Handler handler = dispatcher.AddHandler<EvtTestBase>(_ => count++);

        handler.Dispose();
        await dispatcher.DispatchEventAsync(new EvtTestBase(1));

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task WaitForFirstReturnsFirstCompletedTask()
    {
        var slow = new TaskCompletionSource<int>();
        var fast = new TaskCompletionSource<int>();

        Task<int> first = WaitFor.FirstAsync(slow.Task, fast.Task);
        fast.SetResult(42);

        Assert.Equal(42, await first);
    }

    [Fact]
    public async Task WaitForFirstPropagatesFault()
    {
        var failed = new TaskCompletionSource<int>();
        var pending = new TaskCompletionSource<int>();

        Task<int> first = WaitFor.FirstAsync(failed.Task, pending.Task);
        failed.SetException(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
    }
}
