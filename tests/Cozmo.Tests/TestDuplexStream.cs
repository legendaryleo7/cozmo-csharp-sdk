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

using System.Threading.Channels;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

/// <summary>In-memory duplex stream pair used to exercise the usbmux and CLAD framing.</summary>
internal sealed class DuplexPipeStream : Stream
{
    private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private ChannelWriter<byte[]>? _peerWriter;
    private byte[]? _current;
    private int _currentOffset;

    public static (Stream Client, Stream Server) CreatePair()
    {
        var client = new DuplexPipeStream();
        var server = new DuplexPipeStream();
        client._peerWriter = server._incoming.Writer;
        server._peerWriter = client._incoming.Writer;
        return (client, server);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        while (_current is null || _currentOffset >= _current.Length)
        {
            if (!await _incoming.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            if (_incoming.Reader.TryRead(out byte[]? chunk))
            {
                _current = chunk;
                _currentOffset = 0;
            }
        }

        int count = Math.Min(buffer.Length, _current.Length - _currentOffset);
        _current.AsSpan(_currentOffset, count).CopyTo(buffer.Span);
        _currentOffset += count;
        return count;
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        _peerWriter?.TryWrite(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _incoming.Writer.TryComplete();
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        Dispose(true);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Stream factory that hands the server side of each in-memory pair to the test.</summary>
internal sealed class DuplexServerFactory
{
    private readonly Channel<Stream> _accepted = Channel.CreateUnbounded<Stream>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    public Func<Stream> CreateStream => () =>
    {
        (Stream client, Stream server) = DuplexPipeStream.CreatePair();
        _accepted.Writer.TryWrite(server);
        return client;
    };

    public ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken = default) =>
        _accepted.Reader.ReadAsync(cancellationToken);

    public bool HasPending => _accepted.Reader.Count > 0;
}

/// <summary>Test-side endpoint of a usbmux connection: reads and writes length-prefixed plists.</summary>
internal sealed class TestUsbMuxPeer
{
    private readonly UsbMuxMessageReader _reader = new();
    private readonly byte[] _buffer = new byte[8192];

    public TestUsbMuxPeer(Stream stream)
    {
        Stream = stream;
    }

    public Stream Stream { get; }

    public async Task<Dictionary<string, object?>> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_reader.TryRead(out Dictionary<string, object?>? message))
            {
                return message;
            }

            int read = await Stream.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The client closed the usbmux connection.");
            }

            _reader.Append(_buffer.AsSpan(0, read));
        }
    }

    public async Task WriteAsync(
        IReadOnlyDictionary<string, object?> message,
        CancellationToken cancellationToken = default)
    {
        await WriteRawAsync(UsbMuxProtocol.Encode(message), cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteRawAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        await Stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
