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

/// <summary>Describes an actively-playing animation trigger on a robot.</summary>
/// <remarks>
/// <para>
/// Port of <c>cozmo/anim.py::AnimationTrigger</c>. Asking Cozmo to play an animation
/// trigger makes him pick one of the animations represented by the group, based on his
/// mood or random weighting, so playing the same trigger twice may not play the exact
/// same underlying animation.
/// </para>
/// <para>To play an exact animation, use <see cref="Animation"/> with a name from <see cref="AnimationNames"/>.</para>
/// </remarks>
public sealed class AnimationTrigger : CozmoAction
{
    /// <summary>Creates an animation-trigger action.</summary>
    /// <param name="trigger">An entry of <see cref="Triggers"/>.</param>
    /// <param name="loopCount">Number of iterations to play the animation for.</param>
    /// <param name="useLiftSafe">True to automatically ignore the lift track if Cozmo is carrying a cube.</param>
    /// <param name="ignoreBodyTrack">True to ignore the body track (wheels and treads).</param>
    /// <param name="ignoreHeadTrack">True to ignore the head track.</param>
    /// <param name="ignoreLiftTrack">True to ignore the lift track.</param>
    /// <param name="enableAbortLogging">False to silence abort messages for actions aborted by design.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    public AnimationTrigger(
        CladEnum<global::Anki.Cozmo.AnimationTrigger> trigger,
        uint loopCount,
        bool useLiftSafe,
        bool ignoreBodyTrack,
        bool ignoreHeadTrack,
        bool ignoreLiftTrack,
        bool enableAbortLogging = true,
        ILogger? logger = null)
        : base(enableAbortLogging, logger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        Trigger = trigger;
        LoopCount = loopCount;
        UseLiftSafe = useLiftSafe;
        IgnoreBodyTrack = ignoreBodyTrack;
        IgnoreHeadTrack = ignoreHeadTrack;
        IgnoreLiftTrack = ignoreLiftTrack;
    }

    /// <summary>The animation trigger that was dispatched.</summary>
    public CladEnum<global::Anki.Cozmo.AnimationTrigger> Trigger { get; }

    /// <summary>Number of iterations the animation was requested for.</summary>
    public uint LoopCount { get; }

    /// <summary>True to automatically ignore the lift track if Cozmo is carrying a cube.</summary>
    public bool UseLiftSafe { get; }

    /// <summary>True to ignore the body track (wheels and treads).</summary>
    public bool IgnoreBodyTrack { get; }

    /// <summary>True to ignore the head track.</summary>
    public bool IgnoreHeadTrack { get; }

    /// <summary>True to ignore the lift track.</summary>
    public bool IgnoreLiftTrack { get; }

    /// <inheritdoc />
    public override void Encode(RobotActionUnion action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.playAnimationTrigger = new PlayAnimationTrigger
        {
            trigger = (int)Trigger.Id,
            numLoops = LoopCount,
            useLiftSafe = UseLiftSafe,
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

        return $"{GetType().Name} trigger={Trigger.Name} loop_count={LoopCount} " +
               $"ignore_tracks=[{string.Join(", ", ignored)}] use_lift_safe={UseLiftSafe} state={State}";
    }

    /// <inheritdoc />
    protected override EvtActionCompleted CreateCompletedEvent()
        => new EvtAnimationCompleted(this, State, null, null, Trigger.Name);
}
