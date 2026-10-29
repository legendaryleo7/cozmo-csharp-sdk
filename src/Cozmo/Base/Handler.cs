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

namespace Anki.Cozmo.Sdk;

/// <summary>A registered event handler; dispose it to stop receiving events.</summary>
public sealed class Handler : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private bool _disabled;

    internal Handler(
        Dispatcher dispatcher,
        Type eventType,
        object callback,
        Func<CozmoEvent, Task> invoke,
        IEventFilter? filter,
        bool oneShot,
        bool enabled = true)
    {
        _dispatcher = dispatcher;
        EventType = eventType;
        Callback = callback;
        Invoke = invoke;
        Filter = filter;
        IsOneShot = oneShot;
        _disabled = !enabled;
    }

    /// <summary>Event type the handler was registered for.</summary>
    public Type EventType { get; }

    /// <summary>True when the handler was registered to fire at most once.</summary>
    public bool IsOneShot { get; }

    /// <summary>True while the handler can still be invoked.</summary>
    public bool IsEnabled => !_disabled;

    internal object Callback { get; }

    internal Func<CozmoEvent, Task> Invoke { get; }

    internal IEventFilter? Filter { get; }

    /// <summary>Removes the handler from the dispatcher it was registered with.</summary>
    public void Disable()
    {
        if (_disabled)
        {
            return;
        }

        _disabled = true;
        _dispatcher.RemoveHandler(this);
    }

    /// <inheritdoc />
    public void Dispose() => Disable();
}
