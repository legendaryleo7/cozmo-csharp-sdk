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

using System.Collections;
using Anki.Cozmo.ExternalInterface;

namespace Anki.Cozmo.Sdk;

/// <summary>Holds the set of animation names returned by the engine.</summary>
/// <remarks>
/// <para>
/// Port of <c>cozmo/anim.py::AnimationNames</c>. Animation names are retrieved from the
/// engine when the SDK connects, unlike the compile-time <see cref="Triggers"/>
/// catalogue.
/// </para>
/// <para>
/// <see cref="RefreshAsync"/> clears the set and asks the engine again. Looking a name up
/// with <see cref="Contains"/> while the set is not loaded raises
/// <see cref="AnimationsNotLoadedException"/>, matching Python's <c>__contains__</c>.
/// </para>
/// </remarks>
public sealed class AnimationNames : Dispatcher, IReadOnlyCollection<string>
{
    private readonly CozmoConnection _connection;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly Handler _messageHandler;
    private EvtAnimationsLoaded? _loadedEvent;

    /// <summary>Creates an animation-name set bound to a connection.</summary>
    /// <param name="connection">Connection whose inbound messages are observed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    public AnimationNames(CozmoConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _messageHandler = connection.AddAsyncHandler<EvtMessageReceived>(OnMessageReceivedAsync);
    }

    /// <summary>True once the animation names have been received from the engine.</summary>
    public bool IsLoaded
    {
        get
        {
            lock (_gate)
            {
                return _loadedEvent is not null;
            }
        }
    }

    /// <summary>Number of animation names currently loaded.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _names.Count;
            }
        }
    }

    /// <summary>Snapshot of the currently loaded animation names.</summary>
    public IReadOnlyCollection<string> Names
    {
        get
        {
            lock (_gate)
            {
                return _names.ToArray();
            }
        }
    }

    /// <summary>Tests whether a loaded animation name is in the set.</summary>
    /// <param name="name">Animation name to look up.</param>
    /// <returns>True when the name was reported by the engine.</returns>
    /// <exception cref="AnimationsNotLoadedException">The animation names have not loaded yet.</exception>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            if (_loadedEvent is null)
            {
                throw new AnimationsNotLoadedException("Animations not yet received from engine");
            }

            return _names.Contains(name);
        }
    }

    /// <summary>Requests the list of animation names from the engine.</summary>
    /// <remarks>
    /// Matches Python's <c>refresh</c>: the set is marked unloaded and cleared before the
    /// request is sent, so an animation lookup during a refresh raises
    /// <see cref="AnimationsNotLoadedException"/>. An <see cref="EvtAnimationsLoaded"/>
    /// event is raised once the engine's reply has been received.
    /// </remarks>
    public async Task RefreshAsync()
    {
        lock (_gate)
        {
            _loadedEvent = null;
            _names.Clear();
        }

        await _connection.SendAsync(new MessageGameToEngine
        {
            RequestAvailableAnimations = new RequestAvailableAnimations(),
        }).ConfigureAwait(false);
    }

    /// <summary>Waits for the animation names to be loaded from the engine.</summary>
    /// <param name="timeout">
    /// Maximum time to wait; null waits indefinitely. Use
    /// <see cref="Timeout.InfiniteTimeSpan"/> explicitly to make that intent visible.
    /// </param>
    /// <returns>The <see cref="EvtAnimationsLoaded"/> event, which may have arrived earlier.</returns>
    /// <exception cref="TimeoutException">The names did not load in time.</exception>
    /// <exception cref="SdkShutdownException">The set was disposed before the names loaded.</exception>
    public Task<EvtAnimationsLoaded> WaitForLoadedAsync(TimeSpan? timeout = null)
    {
        // Registering while holding the same lock the message handler takes prevents an
        // end-of-message that arrives between the loaded check and handler registration
        // from being lost.
        lock (_gate)
        {
            if (_loadedEvent is not null)
            {
                return Task.FromResult(_loadedEvent);
            }

            return WaitFor<EvtAnimationsLoaded>(timeout: timeout ?? Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Returns an enumerator over a snapshot of the loaded names.</summary>
    public IEnumerator<string> GetEnumerator() => Names.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _messageHandler.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task OnMessageReceivedAsync(EvtMessageReceived evt)
    {
        switch (evt.Message.tag)
        {
            case MessageEngineToGame.Tag.AnimationAvailable:
                string name = evt.Message.AnimationAvailable.animName;
                lock (_gate)
                {
                    _names.Add(name);
                }

                break;

            case MessageEngineToGame.Tag.EndOfMessage:
                EvtAnimationsLoaded? loaded;
                lock (_gate)
                {
                    if (_loadedEvent is not null)
                    {
                        return;
                    }

                    loaded = new EvtAnimationsLoaded();
                    _loadedEvent = loaded;
                }

                await DispatchEventAsync(loaded).ConfigureAwait(false);
                break;
        }
    }
}
