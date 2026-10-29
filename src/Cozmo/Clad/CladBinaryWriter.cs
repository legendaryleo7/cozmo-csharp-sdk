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

/// <summary>Writes CLAD primitive values to a stream, little-endian.</summary>
public sealed class CladBinaryWriter
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly BinaryWriter _writer;

    /// <summary>Creates a writer over <paramref name="stream"/>; the stream stays open.</summary>
    public CladBinaryWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
    }

    /// <summary>Writes a CLAD <c>int_8</c>.</summary>
    public void WriteInt8(sbyte value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>uint_8</c>.</summary>
    public void WriteUInt8(byte value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>int_16</c>.</summary>
    public void WriteInt16(short value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>uint_16</c>.</summary>
    public void WriteUInt16(ushort value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>int_32</c>.</summary>
    public void WriteInt32(int value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>uint_32</c>.</summary>
    public void WriteUInt32(uint value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>int_64</c>.</summary>
    public void WriteInt64(long value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>uint_64</c>.</summary>
    public void WriteUInt64(ulong value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>float_32</c>.</summary>
    public void WriteSingle(float value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>float_64</c>.</summary>
    public void WriteDouble(double value) => _writer.Write(value);

    /// <summary>Writes a CLAD <c>bool</c> as one byte.</summary>
    public void WriteBool(bool value) => _writer.Write(value ? (byte)1 : (byte)0);

    /// <summary>Writes a string with a <c>uint_8</c> byte-length prefix.</summary>
    public void WriteString8(string? value) => WriteString(value, byte.MaxValue, oneByteLength: true);

    /// <summary>Writes a string with a <c>uint_16</c> byte-length prefix.</summary>
    public void WriteString16(string? value) => WriteString(value, ushort.MaxValue, oneByteLength: false);

    /// <summary>Flushes the underlying stream.</summary>
    public void Flush() => _writer.Flush();

    private void WriteString(string? value, int maxBytes, bool oneByteLength)
    {
        value ??= string.Empty;
        int count = Utf8.GetByteCount(value);
        if (count > maxBytes)
        {
            throw new ArgumentException($"String is too long: {count} bytes exceeds the CLAD limit of {maxBytes}.");
        }

        if (oneByteLength)
        {
            _writer.Write((byte)count);
        }
        else
        {
            _writer.Write((ushort)count);
        }

        _writer.Write(Utf8.GetBytes(value));
    }
}
