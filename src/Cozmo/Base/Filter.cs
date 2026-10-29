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

/// <summary>Non-generic view of an event filter, used by handlers and waits.</summary>
public interface IEventFilter
{
    /// <summary>Event type this filter applies to; subclasses also match.</summary>
    Type EventType { get; }

    /// <summary>True when the event should be delivered.</summary>
    bool Matches(CozmoEvent evt);
}

/// <summary>Factory for strongly typed event filters.</summary>
/// <example>
/// <code>
/// var filter = Filter.For&lt;EvtObjectTapped&gt;().Where(evt =&gt; ReferenceEquals(evt.Obj, cube));
/// </code>
/// </example>
public static class Filter
{
    /// <summary>Creates a filter for an event type.</summary>
    public static Filter<TEvt> For<TEvt>()
        where TEvt : CozmoEvent => new();
}

/// <summary>Predicates that restrict when a handler or wait is triggered.</summary>
/// <typeparam name="TEvt">Event type being filtered.</typeparam>
public sealed class Filter<TEvt> : IEventFilter
    where TEvt : CozmoEvent
{
    private readonly List<Func<TEvt, bool>> _predicates = new();

    /// <inheritdoc />
    public Type EventType => typeof(TEvt);

    /// <summary>Adds a predicate that must match for the filter to match.</summary>
    public Filter<TEvt> Where(Func<TEvt, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicates.Add(predicate);
        return this;
    }

    /// <inheritdoc />
    public bool Matches(CozmoEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt is not TEvt typed)
        {
            return false;
        }

        foreach (Func<TEvt, bool> predicate in _predicates)
        {
            if (!predicate(typed))
            {
                return false;
            }
        }

        return true;
    }
}
