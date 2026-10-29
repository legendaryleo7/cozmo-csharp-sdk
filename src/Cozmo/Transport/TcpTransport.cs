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
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Anki.Cozmo.Sdk;

/// <summary>CLAD transport over a TCP connection, as used by the app's SDK bridge.</summary>
/// <remarks>
/// A background read loop frames inbound bytes and publishes complete frames through a
/// channel. Sends are serialized so two callers cannot interleave a length prefix and body.
/// </remarks>
public sealed class TcpTransport : ICladTransport
{
    private const int ReadBufferSize = 4096;

    private readonly string _host;
    private readonly int _port;
    private readonly Channel<byte[]> _frames = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Task? _readLoop;
    private Exception? _error;
    private bool _closed;

    /// <summary>Creates a transport for a host and port; call <see cref="ConnectAsync"/> first.</summary>
    public TcpTransport(string host, int port)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);
        _host = host;
        _port = port;
    }

    /// <summary>True while the transport has a live connection.</summary>
    public bool IsConnected => !_closed && _stream is not null;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        if (_client is not null)
        {
            throw new InvalidOperationException("The transport is already connected.");
        }

        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            client.Dispose();
            throw new CozmoConnectionException($"Could not connect to {_host}:{_port}.", ex);
        }

        _client = client;
        _stream = client.GetStream();
        _readLoop = ReadLoopAsync(_lifetime.Token);
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (frame.Length > ushort.MaxValue)
        {
            throw new ArgumentException($"A CLAD frame cannot exceed {ushort.MaxValue} bytes.", nameof(frame));
        }

        NetworkStream? stream = _stream;
        if (_closed || stream is null)
        {
            return;
        }

        var packet = new byte[2 + frame.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)frame.Length);
        frame.CopyTo(packet.AsMemory(2));

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
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
            _stream?.Dispose();
        }
        catch (IOException)
        {
            // The socket was already gone; nothing to release.
        }

        _client?.Dispose();
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
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await _stream!.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
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
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            _error = new ConnectionAbortedException("The CLAD connection was lost.", ex);
        }
        finally
        {
            _frames.Writer.TryComplete();
        }
    }
}
