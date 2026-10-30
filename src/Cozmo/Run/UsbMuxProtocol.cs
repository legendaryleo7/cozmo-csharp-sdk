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
using System.Diagnostics.CodeAnalysis;

namespace Anki.Cozmo.Sdk;

/// <summary>Encodes usbmux plist messages with the protocol's 16-byte header.</summary>
internal static class UsbMuxProtocol
{
    /// <summary>Size of the fixed usbmux header that precedes every plist message.</summary>
    public const int HeaderSize = 16;

    /// <summary>Protocol version understood by the client, matching <c>plistlib</c>'s usbmux use.</summary>
    public const uint ProtocolVersion = 1;

    /// <summary>Header request type used for plist messages.</summary>
    public const uint PlistRequestType = 8;

    /// <summary>Header tag value used by the Python client.</summary>
    public const uint DefaultTag = 1;

    private const int MaxMessageSize = 16 * 1024 * 1024;

    /// <summary>Encodes a plist message into a complete usbmux frame.</summary>
    public static byte[] Encode(IReadOnlyDictionary<string, object?> message)
    {
        byte[] payload = UsbMuxPlist.Serialize(message);
        var frame = new byte[HeaderSize + payload.Length];
        Span<byte> header = frame.AsSpan(0, HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], (uint)frame.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), PlistRequestType);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), DefaultTag);
        payload.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    /// <summary>Reads a usbmux header and validates its declared length.</summary>
    public static int ReadFrameLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
        {
            throw new UsbMuxProtocolException("A usbmux frame header is incomplete.");
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(header[..4]);
        if (length < HeaderSize || length > MaxMessageSize)
        {
            throw new UsbMuxProtocolException($"A usbmux frame declared an invalid length of {length} bytes.");
        }

        return (int)length;
    }

    /// <summary>Validates the protocol version in a complete frame header.</summary>
    public static void ValidateVersion(ReadOnlySpan<byte> header)
    {
        if (header.Length < 8)
        {
            throw new UsbMuxProtocolException("A usbmux frame header is incomplete.");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4));
        if (version != ProtocolVersion)
        {
            throw new UsbMuxProtocolException("Unsupported protocol version from usbmux stream");
        }
    }
}

/// <summary>Reassembles length-prefixed usbmux plist messages and retains any trailing bytes.</summary>
internal sealed class UsbMuxMessageReader
{
    private byte[] _buffer = new byte[1024];
    private int _start;
    private int _end;

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

    /// <summary>Number of bytes received but not yet consumed as a complete message.</summary>
    public int RemainingBytes => _end - _start;

    /// <summary>Takes every buffered byte that follows the last parsed message.</summary>
    public byte[] TakeRemaining()
    {
        byte[] remaining = _buffer.AsSpan(_start, _end - _start).ToArray();
        _start = 0;
        _end = 0;
        return remaining;
    }

    /// <summary>Tries to take the next complete usbmux message.</summary>
    public bool TryRead([NotNullWhen(true)] out Dictionary<string, object?>? message)
    {
        message = null;
        int available = _end - _start;
        if (available < UsbMuxProtocol.HeaderSize)
        {
            return false;
        }

        int length = UsbMuxProtocol.ReadFrameLength(_buffer.AsSpan(_start, UsbMuxProtocol.HeaderSize));
        if (available < length)
        {
            return false;
        }

        UsbMuxProtocol.ValidateVersion(_buffer.AsSpan(_start, UsbMuxProtocol.HeaderSize));
        message = UsbMuxPlist.Deserialize(
            _buffer.AsSpan(_start + UsbMuxProtocol.HeaderSize, length - UsbMuxProtocol.HeaderSize));

        _start += length;
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
