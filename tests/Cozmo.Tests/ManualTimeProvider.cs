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

namespace Cozmo.Tests;

/// <summary>Manual clock and timer source used to drive visibility timers deterministically.</summary>
/// <remarks>
/// <see cref="TimeProvider.CreateTimer"/> callbacks fire synchronously from
/// <see cref="Advance"/>, so a test can assert on the dispatched event as soon as the advance
/// returns. Object, face and pet tests use this helper to drive visibility timers without
/// waiting for wall-clock time.
/// </remarks>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _utcNow = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that became due, in due order.</summary>
    /// <param name="delta">The amount of time to advance.</param>
    public void Advance(TimeSpan delta)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _utcNow += delta;
            due = _timers
                .Where(timer => timer.IsDue(_utcNow))
                .OrderBy(timer => timer.DueAt)
                .ToList();
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    private void RemoveTimer(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private bool _disposed;
        private TimeSpan _period;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public DateTimeOffset? DueAt { get; private set; }

        public bool IsDue(DateTimeOffset now) =>
            !_disposed && DueAt is DateTimeOffset dueAt && dueAt <= now;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan
                ? null
                : _owner.GetUtcNow() + dueTime;
            return true;
        }

        public void Fire()
        {
            if (_disposed)
            {
                return;
            }

            DueAt = _period == Timeout.InfiniteTimeSpan
                ? null
                : _owner.GetUtcNow() + _period;
            _callback(_state);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DueAt = null;
            _owner.RemoveTimer(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
