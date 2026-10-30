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

/// <summary>The base type for anything Cozmo can see.</summary>
/// <remarks>
/// <para>
/// An element tracks the time it was last observed, the pose and image box reported with that
/// observation, and whether it is still considered visible.
/// </para>
/// <para>
/// Visibility is timer based: every observation resets a one-shot timer for
/// <see cref="VisibilityTimeout"/>; when it expires the element becomes invisible and raises
/// <see cref="EvtObjectDisappeared"/>. An injected <see cref="TimeProvider"/> controls the
/// timer.
/// </para>
/// </remarks>
public class ObservableElement : Dispatcher
{
    /// <summary>
    /// Length of time to go without receiving an observation before an element is considered
    /// invisible.
    /// </summary>
    public static readonly TimeSpan ObjectVisibilityTimeout = TimeSpan.FromSeconds(0.4);

    private readonly TimeProvider _timeProvider;
    private bool _disposed;
    private bool _isVisible;
    private ITimer? _visibilityTimer;

    /// <summary>Creates an observable element.</summary>
    /// <param name="connection">The connection used to send commands for this element.</param>
    /// <param name="world">The world that owns this element; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="timeProvider">
    /// Clock and timer source. Defaults to <see cref="TimeProvider.System"/>; tests pass a
    /// manual implementation to control visibility expiry.
    /// </param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this element.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    protected ObservableElement(
        CozmoConnection connection,
        IWorldContext? world = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(dispatchParent)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connection = connection;
        World = world;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The connection used to send commands for this element.</summary>
    public CozmoConnection Connection { get; }

    /// <summary>The robot's world in which this element is located.</summary>
    public IWorldContext? World { get; }

    /// <summary>The pose of the element in the world.</summary>
    /// <remarks><see langword="null"/> for elements that do not have pose information yet.</remarks>
    public Pose? Pose { get; protected set; }

    /// <summary>The time the last event was received.</summary>
    /// <remarks><see langword="null"/> if no events have yet been received.</remarks>
    public DateTimeOffset? LastEventTime { get; protected set; }

    /// <summary>The time the element was last observed by the robot.</summary>
    /// <remarks><see langword="null"/> if the element has not yet been observed.</remarks>
    public DateTimeOffset? LastObservedTime { get; protected set; }

    /// <summary>The robot's timestamp of the last observed event, in milliseconds relative to the robot epoch.</summary>
    /// <remarks><see langword="null"/> if the element has not yet been observed.</remarks>
    public uint? LastObservedRobotTimestamp { get; protected set; }

    /// <summary>Where the element was last visible within Cozmo's camera view.</summary>
    /// <remarks><see langword="null"/> if the element has not yet been observed.</remarks>
    public ImageBox? LastObservedImageBox { get; protected set; }

    /// <summary><see langword="true"/> while the element has been observed recently.</summary>
    /// <remarks>
    /// "Recently" is defined as <see cref="VisibilityTimeout"/> seconds. The value becomes
    /// <see langword="true"/> on an observation and returns to <see langword="false"/> when
    /// the visibility timer expires.
    /// </remarks>
    public bool IsVisible => _isVisible;

    /// <summary>
    /// Length of time without an observation after which this element is considered invisible.
    /// </summary>
    /// <remarks>
    /// Subclasses may override this property to use a different timeout.
    /// </remarks>
    public virtual TimeSpan VisibilityTimeout => ObjectVisibilityTimeout;

    /// <summary>The time since this element was last seen.</summary>
    /// <remarks>
    /// Returns <see cref="TimeSpan.MaxValue"/> when the element has never been observed.
    /// </remarks>
    public TimeSpan TimeSinceLastSeen
    {
        get
        {
            if (LastObservedTime is not DateTimeOffset observedTime)
            {
                return TimeSpan.MaxValue;
            }

            return _timeProvider.GetUtcNow() - observedTime;
        }
    }

    /// <summary>Logger used for object diagnostics.</summary>
    protected ILogger Logger { get; }

    /// <summary>Clock and timer source used by this element.</summary>
    protected TimeProvider TimeProvider => _timeProvider;

    /// <summary>
    /// Sets a field only when its value changes and records the field name in
    /// <paramref name="changedFields"/>.
    /// </summary>
    /// <typeparam name="T">Type of the field.</typeparam>
    /// <param name="changedFields">Set of names of fields that have changed.</param>
    /// <param name="fieldName">Field name to record.</param>
    /// <param name="currentValue">The field's current value.</param>
    /// <param name="newValue">The value to assign.</param>
    /// <param name="setter">Assigns <paramref name="newValue"/> to the field.</param>
    /// <param name="comparer">Optional value comparer; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <returns><see langword="true"/> when the value changed and the setter ran.</returns>
    protected static bool UpdateField<T>(
        ISet<string> changedFields,
        string fieldName,
        T currentValue,
        T newValue,
        Action<T> setter,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(changedFields);
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(setter);

        if ((comparer ?? EqualityComparer<T>.Default).Equals(currentValue, newValue))
        {
            return false;
        }

        setter(newValue);
        changedFields.Add(fieldName);
        return true;
    }

    /// <summary>Applies one observation: updates timestamps and raises the observed/appeared events.</summary>
    /// <param name="imageBox">Where the element was visible.</param>
    /// <param name="robotTimestamp">The robot's observation timestamp in milliseconds.</param>
    /// <param name="changedFields">
    /// Field names already changed by the caller; the four observation fields are added before
    /// the events are dispatched.
    /// </param>
    /// <returns>A task that completes after the events have been dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="changedFields"/> is <see langword="null"/>.</exception>
    protected async Task OnObservedAsync(
        ImageBox imageBox,
        uint robotTimestamp,
        HashSet<string> changedFields)
    {
        ArgumentNullException.ThrowIfNull(changedFields);

        bool newlyVisible = !_isVisible;
        _isVisible = true;

        changedFields.Add("last_observed_time");
        changedFields.Add("last_observed_robot_timestamp");
        changedFields.Add("last_event_time");
        changedFields.Add("last_observed_image_box");

        DateTimeOffset now = _timeProvider.GetUtcNow();
        LastObservedTime = now;
        LastObservedRobotTimestamp = robotTimestamp;
        LastEventTime = now;
        LastObservedImageBox = imageBox;
        ResetVisibilityTimer();

        await DispatchObservedEventAsync(changedFields, imageBox).ConfigureAwait(false);
        if (newlyVisible)
        {
            await DispatchAppearedEventAsync(changedFields, imageBox).ConfigureAwait(false);
        }
    }

    /// <summary>Dispatches the event raised for every observation.</summary>
    /// <param name="changedFields">Names of fields updated by the observation.</param>
    /// <param name="imageBox">Where the element was visible.</param>
    /// <returns>A task that completes after dispatch.</returns>
    /// <remarks>Subclasses override this to raise their concrete observed event.</remarks>
    protected virtual Task DispatchObservedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) => Task.CompletedTask;

    /// <summary>Dispatches the event raised when the element first becomes visible.</summary>
    /// <param name="changedFields">Names of fields updated by the observation.</param>
    /// <param name="imageBox">Where the element was visible.</param>
    /// <returns>A task that completes after dispatch.</returns>
    /// <remarks>Subclasses override this to raise their concrete appeared event.</remarks>
    protected virtual Task DispatchAppearedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) => Task.CompletedTask;

    /// <summary>Dispatches the event raised when the visibility timer expires.</summary>
    /// <returns>A task that completes after dispatch.</returns>
    /// <remarks>Subclasses override this to raise their concrete disappeared event.</remarks>
    protected virtual Task DispatchDisappearedEventAsync() => Task.CompletedTask;

    /// <summary>Releases the visibility timer, handlers and child dispatchers.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="IDisposable.Dispose"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            _visibilityTimer?.Dispose();
            _visibilityTimer = null;
        }

        base.Dispose(disposing);
    }

    private void ResetVisibilityTimer()
    {
        _visibilityTimer?.Dispose();
        _visibilityTimer = null;
        if (_disposed)
        {
            return;
        }

        _visibilityTimer = _timeProvider.CreateTimer(
            static state => ((ObservableElement)state!).HandleVisibilityTimeout(),
            this,
            VisibilityTimeout,
            Timeout.InfiniteTimeSpan);
    }

    private void HandleVisibilityTimeout()
    {
        if (_disposed)
        {
            return;
        }

        _isVisible = false;

        // Timer callbacks cannot await. The helper never faults, so the discard is safe.
        _ = DispatchDisappearedSafelyAsync();
    }

    private async Task DispatchDisappearedSafelyAsync()
    {
        try
        {
            await DispatchDisappearedEventAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "A handler failed while dispatching the disappeared event for {Element}.",
                this);
        }
    }
}
