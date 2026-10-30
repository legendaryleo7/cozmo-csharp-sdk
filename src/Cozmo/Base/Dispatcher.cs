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

using System.Runtime.CompilerServices;

namespace Anki.Cozmo.Sdk;

/// <summary>Base class that dispatches events to registered handlers, children and a parent.</summary>
/// <remarks>
/// <para>
/// Dispatch order is handlers registered on this object, then child dispatchers, then the
/// parent dispatcher. Handlers registered for a base event type also
/// receive subclass events. Any handler may throw <see cref="StopPropagationException"/> to
/// stop the remaining handlers.
/// </para>
/// <para>
/// An event is delivered to a given dispatcher at most once, which makes cyclic
/// parent/child wiring safe. All members are intended to be used from a single dispatch
/// context; the class is not thread-safe by design.
/// </para>
/// </remarks>
public class Dispatcher : IDisposable
{
    /// <summary>Default timeout used when a wait does not specify one.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly ConditionalWeakTable<CozmoEvent, HashSet<Dispatcher>> DeliveredTo = new();

    private readonly Dictionary<Type, List<Handler>> _handlers = new();
    private readonly List<Dispatcher> _children = new();
    private readonly List<PendingWait> _waits = new();
    private Dispatcher? _parent;
    private bool _dispatching = true;
    private bool _disposed;

    /// <summary>Creates a dispatcher with no parent.</summary>
    public Dispatcher()
    {
    }

    /// <summary>Creates a dispatcher and registers it as a child of <paramref name="dispatchParent"/>.</summary>
    public Dispatcher(Dispatcher? dispatchParent)
    {
        SetParentDispatcher(dispatchParent);
    }

    /// <summary>True while this dispatcher still delivers events.</summary>
    public bool IsDispatching => _dispatching;

    /// <summary>Sets the dispatcher that receives events relayed from this one.</summary>
    public void SetParentDispatcher(Dispatcher? parent)
    {
        if (ReferenceEquals(_parent, parent))
        {
            return;
        }

        _parent = parent;
    }

    /// <summary>Adds a dispatcher that receives every event dispatched on this one.</summary>
    public void AddChildDispatcher(Dispatcher child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Contains(child))
        {
            _children.Add(child);
        }
    }

    /// <summary>Removes a previously added child dispatcher.</summary>
    public void RemoveChildDispatcher(Dispatcher child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Remove(child);
    }

    /// <summary>Stops delivering events; call before closing a connection to avoid stray events.</summary>
    public void StopDispatching()
    {
        _dispatching = false;
    }

    /// <summary>Registers a synchronous handler for an event type.</summary>
    public Handler AddHandler<TEvt>(Action<TEvt> handler, IEventFilter? filter = null)
        where TEvt : CozmoEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddHandlerCore(
            typeof(TEvt),
            handler,
            evt =>
            {
                handler((TEvt)evt);
                return Task.CompletedTask;
            },
            filter,
            oneShot: false);
    }

    /// <summary>Registers an asynchronous handler for an event type.</summary>
    public Handler AddAsyncHandler<TEvt>(Func<TEvt, Task> handler, IEventFilter? filter = null)
        where TEvt : CozmoEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddHandlerCore(typeof(TEvt), handler, evt => handler((TEvt)evt), filter, oneShot: false);
    }

    /// <summary>Registers a handler that is removed after its first matching event.</summary>
    public Handler AddOneShotHandler<TEvt>(Action<TEvt> handler, IEventFilter? filter = null)
        where TEvt : CozmoEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddHandlerCore(
            typeof(TEvt),
            handler,
            evt =>
            {
                handler((TEvt)evt);
                return Task.CompletedTask;
            },
            filter,
            oneShot: true);
    }

    /// <summary>Registers an asynchronous handler that is removed after its first matching event.</summary>
    public Handler AddOneShotAsyncHandler<TEvt>(Func<TEvt, Task> handler, IEventFilter? filter = null)
        where TEvt : CozmoEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddHandlerCore(typeof(TEvt), handler, evt => handler((TEvt)evt), filter, oneShot: true);
    }

    /// <summary>Removes a previously registered handler by its original callback.</summary>
    /// <exception cref="InvalidOperationException">No matching handler is registered.</exception>
    public void RemoveHandler<TEvt>(Delegate handler)
        where TEvt : CozmoEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryGetValue(typeof(TEvt), out List<Handler>? handlers))
        {
            throw new InvalidOperationException($"No matching handler found for {typeof(TEvt).Name}.");
        }

        foreach (Handler candidate in handlers.ToArray())
        {
            if (candidate.Callback.Equals(handler))
            {
                RemoveHandler(candidate);
                return;
            }
        }

        throw new InvalidOperationException($"No matching handler found for {typeof(TEvt).Name}.");
    }

    /// <summary>Dispatches an event to this object, its children and its parent.</summary>
    public async Task DispatchEventAsync(CozmoEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!_dispatching || _disposed)
        {
            return;
        }

        HashSet<Dispatcher> delivered = DeliveredTo.GetOrCreateValue(evt);
        if (!delivered.Add(this))
        {
            return;
        }

        try
        {
            foreach (Handler handler in CollectHandlers(evt))
            {
                await handler.Invoke(evt).ConfigureAwait(false);
            }

            foreach (Dispatcher child in _children.ToArray())
            {
                await child.DispatchEventAsync(evt).ConfigureAwait(false);
            }

            if (_parent is not null)
            {
                await _parent.DispatchEventAsync(evt).ConfigureAwait(false);
            }
        }
        catch (StopPropagationException)
        {
            // A handler asked to stop delivery; remaining handlers are skipped.
        }
    }

    /// <summary>Waits for the next matching event.</summary>
    /// <param name="filter">Optional predicate the event must satisfy.</param>
    /// <param name="timeout">
    /// Maximum time to wait. <see langword="null"/> uses a 30-second default; pass
    /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// </param>
    /// <exception cref="TimeoutException">No matching event arrived in time.</exception>
    /// <exception cref="SdkShutdownException">The dispatcher had already stopped.</exception>
    public async Task<TEvt> WaitFor<TEvt>(Func<TEvt, bool>? filter = null, TimeSpan? timeout = null)
        where TEvt : CozmoEvent
    {
        CozmoEvent evt = await WaitForCore(
            typeof(TEvt),
            candidate => candidate is TEvt typed && (filter is null || filter(typed)),
            timeout).ConfigureAwait(false);
        return (TEvt)evt;
    }

    /// <summary>Waits for the next event matching <paramref name="filter"/>.</summary>
    /// <param name="filter">Event type and predicates to match.</param>
    /// <param name="timeout">
    /// Maximum time to wait. <see langword="null"/> uses a 30-second default; pass
    /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// </param>
    /// <exception cref="TimeoutException">No matching event arrived in time.</exception>
    /// <exception cref="SdkShutdownException">The dispatcher had already stopped.</exception>
    public Task<CozmoEvent> WaitFor(IEventFilter filter, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return WaitForCore(filter.EventType, filter.Matches, timeout);
    }

    /// <summary>Fails every pending <see cref="WaitFor{TEvt}"/> with the supplied exception.</summary>
    public void AbortPendingWaits(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        foreach (PendingWait wait in _waits.ToArray())
        {
            wait.Fail(exception);
        }

        _waits.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases handlers, children and pending waits.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatching = false;
        _handlers.Clear();
        _children.Clear();
        _waits.Clear();
        _parent = null;
    }

    internal void RemoveHandler(Handler handler)
    {
        if (_handlers.TryGetValue(handler.EventType, out List<Handler>? handlers))
        {
            handlers.Remove(handler);
        }
    }

    private Handler AddHandlerCore(
        Type eventType,
        object callback,
        Func<CozmoEvent, Task> invoke,
        IEventFilter? filter,
        bool oneShot)
    {
        if (!_dispatching || _disposed)
        {
            return new Handler(this, eventType, callback, invoke, filter, oneShot, enabled: false);
        }

        var handler = new Handler(this, eventType, callback, invoke, filter, oneShot, enabled: true);
        if (!_handlers.TryGetValue(eventType, out List<Handler>? handlers))
        {
            handlers = new List<Handler>();
            _handlers[eventType] = handlers;
        }

        handlers.Add(handler);
        return handler;
    }

    private List<Handler> CollectHandlers(CozmoEvent evt)
    {
        var result = new List<Handler>();
        var seen = new HashSet<Handler>();
        for (Type? type = evt.GetType();
             type is not null && typeof(CozmoEvent).IsAssignableFrom(type);
             type = type.BaseType)
        {
            if (!_handlers.TryGetValue(type, out List<Handler>? handlers))
            {
                continue;
            }

            foreach (Handler handler in handlers.ToArray())
            {
                if (!handler.IsEnabled || !seen.Add(handler))
                {
                    continue;
                }

                if (handler.Filter is not null && !handler.Filter.Matches(evt))
                {
                    continue;
                }

                if (handler.IsOneShot)
                {
                    handler.Disable();
                }

                result.Add(handler);
            }
        }

        return result;
    }

    private Task<CozmoEvent> WaitForCore(Type eventType, Func<CozmoEvent, bool> predicate, TimeSpan? timeout)
    {
        var completion = new TaskCompletionSource<CozmoEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        Handler? handler = null;
        PendingWait? pending = null;

        void Cleanup()
        {
            if (pending is not null)
            {
                _waits.Remove(pending);
            }

            handler?.Disable();
        }

        handler = AddHandlerCore(
            eventType,
            completion,
            evt =>
            {
                if (!predicate(evt))
                {
                    return Task.CompletedTask;
                }

                if (completion.TrySetResult(evt))
                {
                    Cleanup();
                }

                return Task.CompletedTask;
            },
            filter: null,
            oneShot: false);

        pending = new PendingWait(
            Cleanup,
            exception =>
            {
                if (completion.TrySetException(exception))
                {
                    Cleanup();
                }
            });

        if (!handler.IsEnabled)
        {
            completion.TrySetException(new SdkShutdownException("Cannot wait for an event: the dispatcher has been stopped."));
            return completion.Task;
        }

        _waits.Add(pending);
        return AwaitWithTimeoutAsync(completion.Task, pending, timeout);
    }

    private static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, PendingWait pending, TimeSpan? timeout)
    {
        TimeSpan effective = timeout ?? DefaultTimeout;
        try
        {
            if (effective == Timeout.InfiniteTimeSpan)
            {
                return await task.ConfigureAwait(false);
            }

            return await task.WaitAsync(effective).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            pending.Cancel();
            throw;
        }
    }

    private sealed class PendingWait
    {
        public PendingWait(Action cancel, Action<Exception> fail)
        {
            Cancel = cancel;
            Fail = fail;
        }

        public Action Cancel { get; }

        public Action<Exception> Fail { get; }
    }
}
