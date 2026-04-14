// -----------------------------------------------------------------------
// <copyright file="SyntheticDebugInfoBuilder.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Test-only helper that lays out a minimal <c>.debug_info</c> byte
/// stream containing one or more compilation units. The builder
/// encodes the CU header in either DWARF 4 or DWARF 5 layout,
/// handles the DWARF32/DWARF64 length-field trick, and
/// back-patches the CU length once the caller has appended the
/// DIE bytes. All raw bytes are produced little-endian to match
/// <see cref="DwarfReader"/>'s fixed byte order.
/// </summary>
internal sealed class SyntheticDebugInfoBuilder
{
    private readonly List<byte> _bytes = new();
    private int _activeCuLengthPosition = -1;
    private int _activeCuContentsStart = -1;
    private DwarfUnitFormat _activeCuUnitFormat;

    public int Position
    {
        get { return _bytes.Count; }
    }

    public SyntheticDebugInfoBuilder StartCompilationUnit(
        DwarfFormat format,
        DwarfUnitFormat unitFormat,
        int addressSize,
        int debugAbbrevOffset)
    {
        if (_activeCuLengthPosition != -1)
        {
            throw new InvalidOperationException(
                "A compilation unit is already open; call EndCompilationUnit first.");
        }

        _activeCuLengthPosition = _bytes.Count;
        _activeCuUnitFormat = unitFormat;

        if (unitFormat == DwarfUnitFormat.Dwarf32)
        {
            AppendU32(0);
        }
        else
        {
            AppendU32(0xFFFFFFFFu);
            AppendU64(0);
        }

        _activeCuContentsStart = _bytes.Count;

        AppendU16((ushort)format);
        if (format == DwarfFormat.Dwarf5)
        {
            AppendU8(1);
            AppendU8((byte)addressSize);
            AppendSectionOffset(unitFormat, (ulong)(uint)debugAbbrevOffset);
        }
        else
        {
            AppendSectionOffset(unitFormat, (ulong)(uint)debugAbbrevOffset);
            AppendU8((byte)addressSize);
        }

        return this;
    }

    public SyntheticDebugInfoBuilder EndCompilationUnit()
    {
        if (_activeCuLengthPosition == -1)
        {
            throw new InvalidOperationException(
                "No compilation unit is currently open.");
        }

        int contentsEnd = _bytes.Count;
        int contentsLength = contentsEnd - _activeCuContentsStart;

        if (_activeCuUnitFormat == DwarfUnitFormat.Dwarf32)
        {
            PatchU32(_activeCuLengthPosition, (uint)contentsLength);
        }
        else
        {
            PatchU64(_activeCuLengthPosition + 4, (ulong)contentsLength);
        }

        _activeCuLengthPosition = -1;
        _activeCuContentsStart = -1;
        return this;
    }

    public SyntheticDebugInfoBuilder AppendU8(byte value)
    {
        _bytes.Add(value);
        return this;
    }

    public SyntheticDebugInfoBuilder AppendU16(ushort value)
    {
        _bytes.Add((byte)(value & 0xFF));
        _bytes.Add((byte)((value >> 8) & 0xFF));
        return this;
    }

    public SyntheticDebugInfoBuilder AppendU32(uint value)
    {
        _bytes.Add((byte)(value & 0xFF));
        _bytes.Add((byte)((value >> 8) & 0xFF));
        _bytes.Add((byte)((value >> 16) & 0xFF));
        _bytes.Add((byte)((value >> 24) & 0xFF));
        return this;
    }

    public SyntheticDebugInfoBuilder AppendU64(ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            _bytes.Add((byte)((value >> shift) & 0xFFu));
        }
        return this;
    }

    public SyntheticDebugInfoBuilder AppendUnsignedLeb128(ulong value)
    {
        while (true)
        {
            byte payload = (byte)(value & 0x7F);
            value >>= 7;
            if (value == 0)
            {
                _bytes.Add(payload);
                return this;
            }
            _bytes.Add((byte)(payload | 0x80));
        }
    }

    public SyntheticDebugInfoBuilder AppendSignedLeb128(long value)
    {
        while (true)
        {
            byte payload = (byte)(value & 0x7F);
            long nextValue = value >> 7;
            bool signBit = (payload & 0x40) != 0;
            bool done =
                (nextValue == 0 && !signBit) ||
                (nextValue == -1 && signBit);
            if (done)
            {
                _bytes.Add(payload);
                return this;
            }
            _bytes.Add((byte)(payload | 0x80));
            value = nextValue;
        }
    }

    public SyntheticDebugInfoBuilder AppendNullTerminatedUtf8(string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        _bytes.AddRange(utf8);
        _bytes.Add(0);
        return this;
    }

    public SyntheticDebugInfoBuilder AppendRawBytes(params byte[] bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    public SyntheticDebugInfoBuilder AppendSectionOffset(
        DwarfUnitFormat unitFormat, ulong value)
    {
        if (unitFormat == DwarfUnitFormat.Dwarf32)
        {
            AppendU32((uint)value);
        }
        else
        {
            AppendU64(value);
        }
        return this;
    }

    public SyntheticDebugInfoBuilder AppendAddress(int addressSize, ulong value)
    {
        if (addressSize == 4)
        {
            AppendU32((uint)value);
        }
        else if (addressSize == 8)
        {
            AppendU64(value);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(addressSize),
                "Only 4- and 8-byte addresses are supported; got " + addressSize + ".");
        }
        return this;
    }

    public byte[] ToArray()
    {
        if (_activeCuLengthPosition != -1)
        {
            throw new InvalidOperationException(
                "A compilation unit is still open; call EndCompilationUnit before ToArray.");
        }
        return _bytes.ToArray();
    }

    private void PatchU32(int position, uint value)
    {
        _bytes[position + 0] = (byte)(value & 0xFF);
        _bytes[position + 1] = (byte)((value >> 8) & 0xFF);
        _bytes[position + 2] = (byte)((value >> 16) & 0xFF);
        _bytes[position + 3] = (byte)((value >> 24) & 0xFF);
    }

    private void PatchU64(int position, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            _bytes[position + (shift / 8)] = (byte)((value >> shift) & 0xFFu);
        }
    }
}
