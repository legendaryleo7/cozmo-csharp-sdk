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

public sealed class CladFramerTests
{
    [Fact]
    public void ReadsSingleFrame()
    {
        var framer = new CladFramer();
        framer.Append(Frame(1, 2, 3));

        Assert.True(framer.TryReadFrame(out byte[] frame));
        Assert.Equal(new byte[] { 1, 2, 3 }, frame);
        Assert.Equal(0, framer.BufferedBytes);
        Assert.False(framer.TryReadFrame(out _));
    }

    [Fact]
    public void WaitsForSplitFrame()
    {
        var framer = new CladFramer();
        byte[] framed = Frame(10, 11, 12, 13, 14);

        framer.Append(framed.AsSpan(0, 1));
        Assert.False(framer.TryReadFrame(out _));

        framer.Append(framed.AsSpan(1, 2));
        Assert.False(framer.TryReadFrame(out _));

        framer.Append(framed.AsSpan(3));
        Assert.True(framer.TryReadFrame(out byte[] frame));
        Assert.Equal(new byte[] { 10, 11, 12, 13, 14 }, frame);
    }

    [Fact]
    public void ReadsCoalescedFrames()
    {
        var framer = new CladFramer();
        framer.Append([.. Frame(1), .. Frame(2, 3)]);

        Assert.True(framer.TryReadFrame(out byte[] first));
        Assert.True(framer.TryReadFrame(out byte[] second));
        Assert.False(framer.TryReadFrame(out _));
        Assert.Equal(new byte[] { 1 }, first);
        Assert.Equal(new byte[] { 2, 3 }, second);
    }

    [Fact]
    public void RejectsZeroLengthFrame()
    {
        var framer = new CladFramer();
        framer.Append([0, 0]);

        Assert.Throws<CladProtocolException>(() => framer.TryReadFrame(out _));
    }

    [Fact]
    public void RejectsOversizedFrame()
    {
        var framer = new CladFramer(16);
        framer.Append([17, 0, 1]);

        Assert.Throws<CladProtocolException>(() => framer.TryReadFrame(out _));
    }

    [Fact]
    public void AcceptsMaximumFrameSize()
    {
        var framer = new CladFramer(16);
        framer.Append(Frame(new byte[16]));

        Assert.True(framer.TryReadFrame(out byte[] frame));
        Assert.Equal(16, frame.Length);
    }

    [Fact]
    public void KeepsBufferedBytesUntilFrameCompletes()
    {
        var framer = new CladFramer();
        framer.Append([5, 0, 1, 2]);

        Assert.Equal(4, framer.BufferedBytes);
        Assert.False(framer.TryReadFrame(out _));
        Assert.Equal(4, framer.BufferedBytes);
    }

    [Fact]
    public void RandomChunkingProducesEveryFrame()
    {
        var random = new Random(1234);
        var expected = new List<byte[]>();
        var stream = new List<byte>();
        for (int i = 0; i < 200; i++)
        {
            var payload = new byte[random.Next(1, 300)];
            random.NextBytes(payload);
            expected.Add(payload);
            stream.AddRange(Frame(payload));
        }

        byte[] data = stream.ToArray();
        var framer = new CladFramer();
        var actual = new List<byte[]>();
        int position = 0;
        while (position < data.Length)
        {
            int chunk = Math.Min(random.Next(1, 8), data.Length - position);
            framer.Append(data.AsSpan(position, chunk));
            position += chunk;
            while (framer.TryReadFrame(out byte[] frame))
            {
                actual.Add(frame);
            }
        }

        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }

        Assert.Equal(0, framer.BufferedBytes);
    }

    [Fact]
    public void TransportRejectsInvalidEndpoints()
    {
        Assert.Throws<ArgumentException>(() => new TcpTransport(string.Empty, 5106));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TcpTransport("127.0.0.1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TcpTransport("127.0.0.1", 70000));
    }

    private static byte[] Frame(params byte[] payload) => Frame((IEnumerable<byte>)payload);

    private static byte[] Frame(IEnumerable<byte> payload)
    {
        byte[] body = payload as byte[] ?? payload.ToArray();
        var framed = new byte[2 + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(framed, (ushort)body.Length);
        body.CopyTo(framed, 2);
        return framed;
    }
}
