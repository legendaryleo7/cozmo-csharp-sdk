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

namespace Anki.Cozmo.Sdk;

/// <summary>Transport is not carrying valid CLAD frames.</summary>
public sealed class CladProtocolException : CozmoConnectionException
{
    /// <summary>Creates an exception with a message.</summary>
    public CladProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public CladProtocolException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Reassembles CLAD frames from a byte stream.</summary>
/// <remarks>
/// The wire format matches <c>cozmo/clad_protocol.py</c>: a two-byte little-endian length
/// followed by that many payload bytes. A single append may contain part of a frame, many
/// frames, or both; a frame may span any number of appends.
/// </remarks>
public sealed class CladFramer
{
    /// <summary>Largest accepted frame payload, matching the historical C# SDK limit.</summary>
    public const int MaxFrameSize = 8192;

    private readonly int _maxFrameSize;
    private byte[] _buffer = new byte[1024];
    private int _start;
    private int _end;

    /// <summary>Creates a framer with the default maximum frame size.</summary>
    public CladFramer()
        : this(MaxFrameSize)
    {
    }

    /// <summary>Creates a framer with an explicit maximum frame size.</summary>
    public CladFramer(int maxFrameSize)
    {
        if (maxFrameSize <= 0 || maxFrameSize > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize));
        }

        _maxFrameSize = maxFrameSize;
    }

    /// <summary>Number of buffered bytes that have not yet formed a frame.</summary>
    public int BufferedBytes => _end - _start;

    /// <summary>Adds received bytes to the reassembly buffer.</summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_end));
        _end += data.Length;
    }

    /// <summary>Tries to take the next complete frame.</summary>
    /// <param name="frame">The payload, without the length prefix.</param>
    /// <returns>True when a frame was produced; false when more bytes are needed.</returns>
    /// <exception cref="CladProtocolException">The buffer does not start with a valid frame.</exception>
    public bool TryReadFrame(out byte[] frame)
    {
        if (_end - _start < 2)
        {
            frame = Array.Empty<byte>();
            return false;
        }

        int size = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(_start, 2));
        if (size == 0)
        {
            throw new CladProtocolException("Received a CLAD frame with a zero length.");
        }

        if (size > _maxFrameSize)
        {
            throw new CladProtocolException(
                $"Received a CLAD frame of {size} bytes, which exceeds the {_maxFrameSize}-byte limit.");
        }

        if (_end - _start < 2 + size)
        {
            frame = Array.Empty<byte>();
            return false;
        }

        frame = _buffer.AsSpan(_start + 2, size).ToArray();
        _start += 2 + size;
        if (_start == _end)
        {
            _start = 0;
            _end = 0;
        }

        return true;
    }

    private void EnsureCapacity(int extra)
    {
        if (_end + extra <= _buffer.Length)
        {
            return;
        }

        int pending = _end - _start;
        if (pending + extra <= _buffer.Length)
        {
            _buffer.AsSpan(_start, pending).CopyTo(_buffer);
            _start = 0;
            _end = pending;
            return;
        }

        int required = pending + extra;
        int capacity = _buffer.Length;
        while (capacity < required)
        {
            capacity *= 2;
        }

        Array.Resize(ref _buffer, capacity);
        _buffer.AsSpan(_start, pending).CopyTo(_buffer);
        _start = 0;
        _end = pending;
    }
}
