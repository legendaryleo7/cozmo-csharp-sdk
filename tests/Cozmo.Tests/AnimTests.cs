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
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;
using ActionResult = Anki.Cozmo.ActionResult;

namespace Cozmo.Tests;

public sealed class AnimTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void AnimationEncodesEveryFlagCombination(bool ignoreBody, bool ignoreHead, bool ignoreLift)
    {
        var animation = new Animation("anim_test", 3, ignoreBody, ignoreHead, ignoreLift);
        var action = new RobotActionUnion();

        animation.Encode(action);

        Assert.Equal(RobotActionUnion.Tag.playAnimation, action.tag);
        PlayAnimation playAnimation = action.playAnimation;
        Assert.Equal("anim_test", playAnimation.animationName);
        Assert.Equal(3u, playAnimation.numLoops);
        Assert.Equal(ignoreBody, playAnimation.ignoreBodyTrack);
        Assert.Equal(ignoreHead, playAnimation.ignoreHeadTrack);
        Assert.Equal(ignoreLift, playAnimation.ignoreLiftTrack);
    }

    [Fact]
    public void AnimationExposesItsConstructorValues()
    {
        var animation = new Animation("anim_name", 4, ignoreBodyTrack: true, ignoreLiftTrack: true);

        Assert.Equal("anim_name", animation.AnimName);
        Assert.Equal(4u, animation.LoopCount);
        Assert.True(animation.IgnoreBodyTrack);
        Assert.False(animation.IgnoreHeadTrack);
        Assert.True(animation.IgnoreLiftTrack);
        Assert.Contains("anim_name", animation.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnimationEncodesNullUnionThrows()
    {
        var animation = new Animation("anim_name", 1);

        Assert.Throws<ArgumentNullException>(() => animation.Encode(null!));
    }

    [Fact]
    public void AnimationTriggerEncodesFields()
    {
        var animationTrigger = new AnimationTrigger(
            Triggers.AcknowledgeFaceNamed,
            loopCount: 2,
            useLiftSafe: true,
            ignoreBodyTrack: true,
            ignoreHeadTrack: false,
            ignoreLiftTrack: true);
        var action = new RobotActionUnion();

        animationTrigger.Encode(action);

        Assert.Equal(RobotActionUnion.Tag.playAnimationTrigger, action.tag);
        PlayAnimationTrigger playAnimationTrigger = action.playAnimationTrigger;
        Assert.Equal(1, playAnimationTrigger.trigger);
        Assert.Equal(2u, playAnimationTrigger.numLoops);
        Assert.True(playAnimationTrigger.useLiftSafe);
        Assert.True(playAnimationTrigger.ignoreBodyTrack);
        Assert.False(playAnimationTrigger.ignoreHeadTrack);
        Assert.True(playAnimationTrigger.ignoreLiftTrack);
        Assert.Equal("AcknowledgeFaceNamed", animationTrigger.Trigger.Name);
    }

    [Fact]
    public async Task AnimationCompletionCarriesAnimationName()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        using var dispatcher = new ActionDispatcher(connection);
        var animation = new Animation("anim_completed", 1);
        await dispatcher.SendSingleActionAsync(animation);
        Task<EvtActionCompleted> completed = animation.WaitForCompletedAsync(TestTimeout);

        await dispatcher.HandleCompletedMessageAsync(Completed(animation, ActionResult.SUCCESS));

        EvtActionCompleted evt = await completed;
        var animationEvt = Assert.IsType<EvtAnimationCompleted>(evt);
        Assert.Same(animation, animationEvt.Action);
        Assert.Equal("anim_completed", animationEvt.AnimationName);
        Assert.Equal(ActionState.Succeeded, animationEvt.State);
        Assert.Null(animationEvt.FailureCode);
        Assert.Null(animationEvt.FailureReason);
        Assert.Same(animationEvt, animation.CompletedEvent);
    }

    [Fact]
    public async Task AnimationTriggerCompletionCarriesTriggerName()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        using var dispatcher = new ActionDispatcher(connection);
        var animationTrigger = new AnimationTrigger(
            Triggers.ReactToCliff,
            loopCount: 1,
            useLiftSafe: false,
            ignoreBodyTrack: false,
            ignoreHeadTrack: false,
            ignoreLiftTrack: false);
        await dispatcher.SendSingleActionAsync(animationTrigger);
        Task<EvtActionCompleted> completed = animationTrigger.WaitForCompletedAsync(TestTimeout);

        await dispatcher.HandleCompletedMessageAsync(Completed(animationTrigger, ActionResult.SUCCESS));

        var animationEvt = Assert.IsType<EvtAnimationCompleted>(await completed);
        Assert.Same(animationTrigger, animationEvt.Action);
        Assert.Equal("ReactToCliff", animationEvt.AnimationName);
    }

    [Fact]
    public void AnimationCompletedFilterMatchesOnlyAnimationActions()
    {
        Filter<EvtActionCompleted> filter = Animation.AnimationCompletedFilter();
        var animationEvt = new EvtAnimationCompleted(
            new Animation("a", 1), ActionState.Succeeded, null, null, "a");
        var triggerEvt = new EvtAnimationCompleted(
            new AnimationTrigger(Triggers.Count, 1, false, false, false, false),
            ActionState.Succeeded,
            null,
            null,
            "Count");

        Assert.True(filter.Matches(animationEvt));
        Assert.False(filter.Matches(triggerEvt));
    }

    [Fact]
    public void TriggersCatalogueMirrorsEveryCladTrigger()
    {
        FieldInfo[] fields = typeof(Triggers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(CladEnum<global::Anki.Cozmo.AnimationTrigger>))
            .ToArray();

        Assert.Equal(576, fields.Length);
        Assert.Equal(576, Triggers.TriggerList.Count);
        Assert.Equal(575, Triggers.Count.Id);
        Assert.Same(Triggers.Count, Triggers.TriggerList[^1]);

        foreach (FieldInfo field in fields)
        {
            var entry = (CladEnum<global::Anki.Cozmo.AnimationTrigger>)field.GetValue(null)!;
            Assert.Equal(field.Name, entry.Name);
            var cladValue = Enum.Parse<global::Anki.Cozmo.AnimationTrigger>(entry.Name);
            Assert.Equal((long)cladValue, entry.Id);
            Assert.Equal(cladValue, entry.Value);
        }

        Assert.Equal(0, Triggers.AcknowledgeFaceInitPause.Id);
        Assert.Equal(1, Triggers.AcknowledgeFaceNamed.Id);
        Assert.Same(Triggers.AcknowledgeFaceNamed, Triggers.TriggerList[1]);
    }

    [Fact]
    public async Task AnimationNamesRefreshSendsRequestAndClearsState()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        using var names = new AnimationNames(connection);

        await names.RefreshAsync();

        Assert.False(names.IsLoaded);
        Assert.Empty(names);
        Assert.Empty(names.Names);
        Assert.Throws<AnimationsNotLoadedException>(() => names.Contains("Idle"));
        Assert.Single(transport.SentFrames);
        MessageGameToEngine sent = DecodeGameToEngine(transport.SentFrames[0]);
        Assert.Equal(MessageGameToEngine.Tag.RequestAvailableAnimations, sent.tag);
    }

    [Fact]
    public async Task AnimationNamesContainsBeforeLoadThrows()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        using var names = new AnimationNames(connection);

        var exception = Assert.Throws<AnimationsNotLoadedException>(() => names.Contains("Idle"));
        Assert.Contains("Animations not yet received from engine", exception.Message, StringComparison.Ordinal);
        Assert.False(names.IsLoaded);
    }

    [Fact]
    public async Task AnimationNamesLoadsFromEngineStream()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        using var names = new AnimationNames(connection);
        Task<EvtAnimationsLoaded> eventWait = names.WaitFor<EvtAnimationsLoaded>(timeout: TestTimeout);

        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            AnimationAvailable = new AnimationAvailable("anim_a"),
        }));
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            AnimationAvailable = new AnimationAvailable("anim_b"),
        }));
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            EndOfMessage = new EndOfMessage(),
        }));

        EvtAnimationsLoaded loaded = await names.WaitForLoadedAsync(TestTimeout);
        EvtAnimationsLoaded handled = await eventWait;

        Assert.True(names.IsLoaded);
        Assert.Same(loaded, handled);
        Assert.Equal(2, names.Count);
        Assert.True(names.Contains("anim_a"));
        Assert.True(names.Contains("anim_b"));
        Assert.False(names.Contains("anim_c"));
        Assert.Contains("anim_a", names.Names);
        Assert.Contains("anim_b", names.Names);
    }

    [Fact]
    public async Task AnimationNamesRefreshClearsPreviouslyLoadedNames()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        using var names = new AnimationNames(connection);
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            AnimationAvailable = new AnimationAvailable("old_anim"),
        }));
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame { EndOfMessage = new EndOfMessage() }));
        await names.WaitForLoadedAsync(TestTimeout);

        await names.RefreshAsync();

        Assert.False(names.IsLoaded);
        Assert.Empty(names);
        Assert.Throws<AnimationsNotLoadedException>(() => names.Contains("old_anim"));

        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            AnimationAvailable = new AnimationAvailable("new_anim"),
        }));
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame { EndOfMessage = new EndOfMessage() }));
        await names.WaitForLoadedAsync(TestTimeout);

        Assert.True(names.Contains("new_anim"));
        Assert.False(names.Contains("old_anim"));
    }

    [Fact]
    public async Task AnimationNamesWaitForLoadedReturnsStoredEvent()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        using var names = new AnimationNames(connection);
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame { EndOfMessage = new EndOfMessage() }));

        EvtAnimationsLoaded first = await names.WaitForLoadedAsync(TestTimeout);
        EvtAnimationsLoaded second = await names.WaitForLoadedAsync(TestTimeout);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task AnimationNamesWaitForLoadedTimesOut()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        using var names = new AnimationNames(connection);

        await Assert.ThrowsAsync<TimeoutException>(
            () => names.WaitForLoadedAsync(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task AnimationNamesDisposeUnsubscribesFromConnection()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();
        var names = new AnimationNames(connection);
        names.Dispose();

        // A ping behind the end-of-message proves the read loop handled the frame; the
        // disposed handler must not have marked the names loaded.
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame { EndOfMessage = new EndOfMessage() }));
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            Ping = new Ping { counter = 7 },
        }));

        await WaitUntilAsync(() => transport.SentFrames.Count >= 1);
        Assert.False(names.IsLoaded);
        Assert.Empty(names);
    }

    private static RobotCompletedAction Completed(CozmoAction action, ActionResult result) => new()
    {
        idTag = action.Id ?? throw new InvalidOperationException("The action has no id."),
        result = (uint)result,
    };

    private static byte[] PackEngineToGame(MessageEngineToGame message)
    {
        using var stream = new MemoryStream();
        message.Pack(new CladBinaryWriter(stream));
        return stream.ToArray();
    }

    private static MessageGameToEngine DecodeGameToEngine(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TestTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the deadline.");
            }

            await Task.Delay(5);
        }
    }
}
