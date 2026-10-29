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

using System.Reflection;
using Anki.Cozmo.Sdk;
using ExecutableBehaviorType = Anki.Cozmo.ExecutableBehaviorType;

namespace Cozmo.Tests;

public sealed class BehaviorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void BehaviorTypesExposePythonSubset()
    {
        FieldInfo[] publicFields = typeof(BehaviorTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(CladEnum<ExecutableBehaviorType>))
            .ToArray();

        Assert.Equal(
            new[]
            {
                "FindFaces",
                "KnockOverCubes",
                "LookAroundInPlace",
                "PounceOnMotion",
                "RollBlock",
                "StackBlocks",
            },
            publicFields.Select(field => field.Name));

        foreach (FieldInfo field in publicFields)
        {
            var entry = (CladEnum<ExecutableBehaviorType>)field.GetValue(null)!;
            Assert.Equal(field.Name, entry.Name);
            var cladValue = Enum.Parse<ExecutableBehaviorType>(entry.Name);
            Assert.Equal((long)cladValue, entry.Id);
            Assert.Equal(cladValue, entry.Value);
        }

        // Python keeps face enrollment private (behavior.py:203); the internal entry is
        // still available inside the assembly for the face-enrollment port.
        Assert.Equal(0, BehaviorTypes.EnrollFace.Id);
        Assert.Equal("EnrollFace", BehaviorTypes.EnrollFace.Name);
    }

    [Fact]
    public void BehaviorStartsIdle()
    {
        var behavior = new Behavior(BehaviorTypes.FindFaces);

        Assert.Equal(BehaviorState.Idle, behavior.State);
        Assert.False(behavior.IsActive);
        Assert.False(behavior.IsRunning);
        Assert.False(behavior.IsCompleted);
        Assert.Equal("FindFaces", behavior.BehaviorTypeName);
        Assert.Contains("FindFaces", behavior.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestStartAndStopDispatchEvents()
    {
        bool stopRequested = false;
        var behavior = new Behavior(
            BehaviorTypes.LookAroundInPlace,
            () =>
            {
                stopRequested = true;
                return Task.CompletedTask;
            });

        Task<EvtBehaviorRequested> requested = behavior.WaitFor<EvtBehaviorRequested>(timeout: TestTimeout);
        await behavior.RequestAsync();

        EvtBehaviorRequested requestedEvt = await requested;
        Assert.Same(behavior, requestedEvt.Behavior);
        Assert.Equal("LookAroundInPlace", requestedEvt.BehaviorTypeName);
        Assert.Equal(BehaviorState.Requested, behavior.State);
        Assert.True(behavior.IsActive);
        Assert.False(behavior.IsRunning);

        Task<EvtBehaviorStarted> started = behavior.WaitFor<EvtBehaviorStarted>(timeout: TestTimeout);
        await behavior.NotifyStartedAsync();

        EvtBehaviorStarted startedEvt = await started;
        Assert.Same(behavior, startedEvt.Behavior);
        Assert.Equal("LookAroundInPlace", startedEvt.BehaviorTypeName);
        Assert.True(behavior.IsRunning);
        Assert.True(behavior.IsActive);
        Assert.False(behavior.IsCompleted);
        await behavior.WaitForStartedAsync(TestTimeout);

        Task<EvtBehaviorStopped> stopped = behavior.WaitFor<EvtBehaviorStopped>(timeout: TestTimeout);
        await behavior.StopAsync();

        EvtBehaviorStopped stoppedEvt = await stopped;
        Assert.Same(behavior, stoppedEvt.Behavior);
        Assert.Equal("LookAroundInPlace", stoppedEvt.BehaviorTypeName);
        Assert.True(stopRequested);
        Assert.True(behavior.IsCompleted);
        Assert.False(behavior.IsActive);
        Assert.False(behavior.IsRunning);
        await behavior.WaitForCompletedAsync(TestTimeout);
    }

    [Fact]
    public async Task StopIdleBehaviorIsNoOp()
    {
        bool stopRequested = false;
        var behavior = new Behavior(
            BehaviorTypes.FindFaces,
            () =>
            {
                stopRequested = true;
                return Task.CompletedTask;
            });
        bool stoppedEventSeen = false;
        behavior.AddHandler<EvtBehaviorStopped>(_ => stoppedEventSeen = true);

        await behavior.StopAsync();

        Assert.False(stopRequested);
        Assert.False(stoppedEventSeen);
        Assert.Equal(BehaviorState.Idle, behavior.State);
    }

    [Fact]
    public async Task StartedWhenNotRequestedIsIgnored()
    {
        var behavior = new Behavior(BehaviorTypes.FindFaces);
        bool startedEventSeen = false;
        behavior.AddHandler<EvtBehaviorStarted>(_ => startedEventSeen = true);

        await behavior.NotifyStartedAsync();

        Assert.False(startedEventSeen);
        Assert.Equal(BehaviorState.Idle, behavior.State);
    }

    [Fact]
    public async Task StartedWhileAlreadyRunningDoesNotRedispatch()
    {
        var behavior = new Behavior(BehaviorTypes.FindFaces);
        await behavior.RequestAsync();
        await behavior.NotifyStartedAsync();
        bool startedEventSeen = false;
        behavior.AddHandler<EvtBehaviorStarted>(_ => startedEventSeen = true);

        await behavior.NotifyStartedAsync();

        Assert.False(startedEventSeen);
        Assert.True(behavior.IsRunning);
    }

    [Fact]
    public async Task WaitForStartedTimesOutForIdleBehavior()
    {
        var behavior = new Behavior(BehaviorTypes.FindFaces);

        await Assert.ThrowsAsync<TimeoutException>(
            () => behavior.WaitForStartedAsync(TimeSpan.FromMilliseconds(50)));
    }
}
