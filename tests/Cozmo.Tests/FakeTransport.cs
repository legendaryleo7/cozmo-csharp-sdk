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
using System.Threading.Channels;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

/// <summary>Deterministic in-memory transport: scripted inbound frames, captured outbound frames.</summary>
public sealed class FakeTransport : ICladTransport
{
    private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> _sent = new();
    private readonly object _gate = new();

    /// <summary>True once <see cref="ConnectAsync"/> has been called.</summary>
    public bool ConnectCalled { get; private set; }

    /// <summary>True once <see cref="CloseAsync"/> has been called.</summary>
    public bool CloseCalled { get; private set; }

    /// <summary>Raised after a frame has been recorded by <see cref="SendAsync"/>.</summary>
    public event Action<byte[]>? FrameSent;

    /// <summary>Frames captured from <see cref="SendAsync"/>, in order.</summary>
    public IReadOnlyList<byte[]> SentFrames
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ConnectCalled = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (CloseCalled)
        {
            return Task.CompletedTask;
        }

        byte[] copy = frame.ToArray();
        lock (_gate)
        {
            _sent.Add(copy);
        }

        FrameSent?.Invoke(copy);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<byte[]> ReceiveFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (byte[] frame in _inbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    /// <inheritdoc />
    public Task CloseAsync()
    {
        CloseCalled = true;
        _inbound.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Queues one complete frame payload (no transport length prefix) for the consumer.</summary>
    public void EnqueueFrame(byte[] frame) => _inbound.Writer.TryWrite(frame);

    /// <summary>Signals the end of the inbound stream.</summary>
    public void CompleteInbound() => _inbound.Writer.TryComplete();

    /// <summary>Signals an inbound failure.</summary>
    public void FailInbound(Exception exception) => _inbound.Writer.TryComplete(exception);
}
