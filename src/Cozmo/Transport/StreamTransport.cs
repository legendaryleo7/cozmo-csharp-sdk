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

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Anki.Cozmo.Sdk;

/// <summary>CLAD transport over an arbitrary duplex stream, used after a usbmux connection.</summary>
/// <remarks>
/// The stream is already connected when the transport is created; <see cref="ConnectAsync"/>
/// only starts the read loop. Bytes that a caller has already read from the stream (for
/// example a usbmux response read ahead of the CLAD payload) can be supplied as a prefix.
/// </remarks>
public sealed class StreamTransport : ICladTransport
{
    private const int ReadBufferSize = 4096;

    private readonly Stream _stream;
    private readonly byte[] _prefetched;
    private readonly Channel<byte[]> _frames = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _readLoop;
    private Exception? _error;
    private bool _connected;
    private bool _closed;

    /// <summary>Creates a transport over a connected stream.</summary>
    /// <param name="stream">Duplex stream carrying raw CLAD length-prefixed frames.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    public StreamTransport(Stream stream)
        : this(stream, ReadOnlyMemory<byte>.Empty)
    {
    }

    internal StreamTransport(Stream stream, ReadOnlyMemory<byte> prefetched)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _prefetched = prefetched.ToArray();
    }

    /// <summary>True once the read loop has been started and the transport is not closed.</summary>
    public bool IsConnected => _connected && !_closed;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        if (_connected)
        {
            throw new InvalidOperationException("The transport is already connected.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _connected = true;
        _readLoop = ReadLoopAsync(_lifetime.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (frame.Length > ushort.MaxValue)
        {
            throw new ArgumentException($"A CLAD frame cannot exceed {ushort.MaxValue} bytes.", nameof(frame));
        }

        if (_closed)
        {
            return;
        }

        var packet = new byte[2 + frame.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)frame.Length);
        frame.CopyTo(packet.AsMemory(2));

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
        {
            throw new ConnectionAbortedException("Failed to send a CLAD frame.", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<byte[]> ReceiveFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChannelReader<byte[]> reader = _frames.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out byte[]? frame))
            {
                yield return frame;
            }
        }

        if (_error is not null)
        {
            throw _error;
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        try
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
        {
            // The stream was already gone; nothing to release.
        }

        _frames.Writer.TryComplete();

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during teardown.
            }
        }

        _sendLock.Dispose();
        _lifetime.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var framer = new CladFramer();
        var buffer = new byte[ReadBufferSize];
        try
        {
            if (_prefetched.Length > 0)
            {
                framer.Append(_prefetched);
                while (framer.TryReadFrame(out byte[] prefetchedFrame))
                {
                    await _frames.Writer.WriteAsync(prefetchedFrame, cancellationToken).ConfigureAwait(false);
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await _stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                framer.Append(buffer.AsSpan(0, read));
                while (framer.TryReadFrame(out byte[] frame))
                {
                    await _frames.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during teardown.
        }
        catch (CladProtocolException ex)
        {
            _error = ex;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
        {
            _error = new ConnectionAbortedException("The CLAD connection was lost.", ex);
        }
        finally
        {
            _frames.Writer.TryComplete();
        }
    }
}
