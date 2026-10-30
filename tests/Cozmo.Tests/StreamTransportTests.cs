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
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

public sealed class StreamTransportTests
{
    [Fact]
    public async Task SendPrefixesLittleEndianLengthAndRoundTripsThroughCladFramer()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        (Stream client, Stream server) = DuplexPipeStream.CreatePair();
        await using var transport = new StreamTransport(client);
        await transport.ConnectAsync(cancellation.Token);

        await transport.SendAsync(new byte[] { 0xAA, 0xBB }, cancellation.Token);
        await transport.SendAsync(new byte[] { 0xCC }, cancellation.Token);

        var received = new byte[7];
        await server.ReadExactlyAsync(received, cancellation.Token);

        var framer = new CladFramer();
        framer.Append(received);
        Assert.True(framer.TryReadFrame(out byte[] first));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, first);
        Assert.True(framer.TryReadFrame(out byte[] second));
        Assert.Equal(new byte[] { 0xCC }, second);
        Assert.False(framer.TryReadFrame(out _));

        await transport.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task ReceiveYieldsFramesAcrossCoalescedAndSplitWrites()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        (Stream client, Stream server) = DuplexPipeStream.CreatePair();
        await using var transport = new StreamTransport(client);
        await transport.ConnectAsync(cancellation.Token);

        await server.WriteAsync(Frame(1, 2).Concat(Frame(3, 4)).ToArray(), cancellation.Token);
        byte[] split = Frame(5, 6, 7);
        await server.WriteAsync(split.AsMemory(0, 3), cancellation.Token);
        await server.WriteAsync(split.AsMemory(3), cancellation.Token);

        var frames = new List<byte[]>();
        await using (IAsyncEnumerator<byte[]> enumerator =
            transport.ReceiveFramesAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token))
        {
            while (frames.Count < 3 && await enumerator.MoveNextAsync())
            {
                frames.Add(enumerator.Current);
            }
        }

        Assert.Equal(3, frames.Count);
        Assert.Equal(new byte[] { 1, 2 }, frames[0]);
        Assert.Equal(new byte[] { 3, 4 }, frames[1]);
        Assert.Equal(new byte[] { 5, 6, 7 }, frames[2]);

        await transport.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task PrefetchedBytesAreFramedBeforeStreamReads()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        (Stream client, Stream server) = DuplexPipeStream.CreatePair();
        await using var transport = new StreamTransport(client, Frame(9, 8));
        await transport.ConnectAsync(cancellation.Token);

        await using (IAsyncEnumerator<byte[]> enumerator =
            transport.ReceiveFramesAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token))
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal(new byte[] { 9, 8 }, enumerator.Current);
        }

        await server.WriteAsync(Frame(7), cancellation.Token);
        await using (IAsyncEnumerator<byte[]> enumerator =
            transport.ReceiveFramesAsync(cancellation.Token).GetAsyncEnumerator(cancellationToken: cancellation.Token))
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal(new byte[] { 7 }, enumerator.Current);
        }

        await transport.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task OversizedFrameIsReportedToTheReceiver()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        (Stream client, Stream server) = DuplexPipeStream.CreatePair();
        await using var transport = new StreamTransport(client);
        await transport.ConnectAsync(cancellation.Token);

        await server.WriteAsync(new byte[] { 0x28, 0x23 }, cancellation.Token);

        await Assert.ThrowsAsync<CladProtocolException>(async () =>
        {
            await using IAsyncEnumerator<byte[]> enumerator =
                transport.ReceiveFramesAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
            await enumerator.MoveNextAsync();
        });

        await transport.DisposeAsync();
        await server.DisposeAsync();
    }

    private static byte[] Frame(params byte[] payload)
    {
        var frame = new byte[payload.Length + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)payload.Length);
        payload.CopyTo(frame.AsSpan(2));
        return frame;
    }
}
