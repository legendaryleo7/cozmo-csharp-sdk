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

namespace Anki.Cozmo.Sdk;

/// <summary>Describes an actively-playing animation on a robot.</summary>
/// <remarks>
/// Send an instance with <see cref="ActionDispatcher.SendSingleActionAsync"/> to play a
/// named animation.
/// </remarks>
public sealed class Animation : CozmoAction
{
    /// <summary>Creates an animation action.</summary>
    /// <param name="animName">Name of the animation to play.</param>
    /// <param name="loopCount">Number of iterations to play the animation for.</param>
    /// <param name="ignoreBodyTrack">True to ignore the body track (wheels and treads).</param>
    /// <param name="ignoreHeadTrack">True to ignore the head track.</param>
    /// <param name="ignoreLiftTrack">True to ignore the lift track.</param>
    /// <param name="enableAbortLogging">False to silence abort messages for actions aborted by design.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    public Animation(
        string animName,
        uint loopCount,
        bool ignoreBodyTrack = false,
        bool ignoreHeadTrack = false,
        bool ignoreLiftTrack = false,
        bool enableAbortLogging = true,
        ILogger? logger = null)
        : base(enableAbortLogging, logger)
    {
        ArgumentNullException.ThrowIfNull(animName);
        AnimName = animName;
        LoopCount = loopCount;
        IgnoreBodyTrack = ignoreBodyTrack;
        IgnoreHeadTrack = ignoreHeadTrack;
        IgnoreLiftTrack = ignoreLiftTrack;
    }

    /// <summary>Name of the animation that was dispatched.</summary>
    public string AnimName { get; }

    /// <summary>Number of iterations the animation was requested for.</summary>
    public uint LoopCount { get; }

    /// <summary>True to ignore the body track (wheels and treads).</summary>
    public bool IgnoreBodyTrack { get; }

    /// <summary>True to ignore the head track.</summary>
    public bool IgnoreHeadTrack { get; }

    /// <summary>True to ignore the lift track.</summary>
    public bool IgnoreLiftTrack { get; }

    /// <summary>Creates a filter that matches completion events for animations only.</summary>
    public static Filter<EvtActionCompleted> AnimationCompletedFilter()
        => Filter.For<EvtActionCompleted>().Where(evt => evt.Action is Animation);

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.playAnimation = new PlayAnimation
        {
            animationName = AnimName,
            numLoops = LoopCount,
            ignoreBodyTrack = IgnoreBodyTrack,
            ignoreHeadTrack = IgnoreHeadTrack,
            ignoreLiftTrack = IgnoreLiftTrack,
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var ignored = new List<string>();
        if (IgnoreBodyTrack)
        {
            ignored.Add("body");
        }

        if (IgnoreHeadTrack)
        {
            ignored.Add("head");
        }

        if (IgnoreLiftTrack)
        {
            ignored.Add("lift");
        }

        return $"{GetType().Name} anim_name={AnimName} loop_count={LoopCount} " +
               $"ignore_tracks=[{string.Join(", ", ignored)}] state={State}";
    }

    /// <inheritdoc />
    protected override EvtActionCompleted CreateCompletedEvent()
        => new EvtAnimationCompleted(this, State, null, null, AnimName);
}
