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

using System;
using System.IO;
using System.Text;

namespace Anki.Cozmo.Sdk.Clad;

/// <summary>Reads CLAD primitive values from a stream, little-endian.</summary>
public sealed class CladBinaryReader
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly BinaryReader _reader;

    /// <summary>Creates a reader over <paramref name="stream"/>; the stream stays open.</summary>
    public CladBinaryReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _reader = new BinaryReader(stream, Utf8, leaveOpen: true);
    }

    /// <summary>Number of bytes consumed so far.</summary>
    public long Position => _reader.BaseStream.Position;

    /// <summary>Reads a CLAD <c>int_8</c>.</summary>
    public sbyte ReadInt8() => _reader.ReadSByte();

    /// <summary>Reads a CLAD <c>uint_8</c>.</summary>
    public byte ReadUInt8() => _reader.ReadByte();

    /// <summary>Reads a CLAD <c>int_16</c>.</summary>
    public short ReadInt16() => _reader.ReadInt16();

    /// <summary>Reads a CLAD <c>uint_16</c>.</summary>
    public ushort ReadUInt16() => _reader.ReadUInt16();

    /// <summary>Reads a CLAD <c>int_32</c>.</summary>
    public int ReadInt32() => _reader.ReadInt32();

    /// <summary>Reads a CLAD <c>uint_32</c>.</summary>
    public uint ReadUInt32() => _reader.ReadUInt32();

    /// <summary>Reads a CLAD <c>int_64</c>.</summary>
    public long ReadInt64() => _reader.ReadInt64();

    /// <summary>Reads a CLAD <c>uint_64</c>.</summary>
    public ulong ReadUInt64() => _reader.ReadUInt64();

    /// <summary>Reads a CLAD <c>float_32</c>.</summary>
    public float ReadSingle() => _reader.ReadSingle();

    /// <summary>Reads a CLAD <c>float_64</c>.</summary>
    public double ReadDouble() => _reader.ReadDouble();

    /// <summary>Reads a CLAD <c>bool</c> stored as one byte.</summary>
    public bool ReadBool() => _reader.ReadByte() != 0;

    /// <summary>Reads a string with a <c>uint_8</c> byte-length prefix.</summary>
    public string ReadString8() => ReadString(_reader.ReadByte());

    /// <summary>Reads a string with a <c>uint_16</c> byte-length prefix.</summary>
    public string ReadString16() => ReadString(_reader.ReadUInt16());

    private string ReadString(int count)
    {
        if (count == 0)
        {
            return string.Empty;
        }

        byte[] bytes = _reader.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException($"Expected {count} string bytes but only {bytes.Length} remain.");
        }

        return Utf8.GetString(bytes);
    }
}
