// -----------------------------------------------------------------------
// <copyright file="SyntheticAbbreviationTableBuilder.cs" company="Ziad">
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

using System.Collections.Generic;
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Test-only helper that lays out valid (or deliberately-malformed)
/// <c>.debug_abbrev</c> byte streams. The parser under test is the
/// only thing the production build uses to decode these bytes, so
/// the encoder lives in the test project and never ships — keeping
/// the "what the parser consumes" and "what a compiler emits"
/// definitions tied to a single, hand-auditable place.
/// </summary>
internal sealed class SyntheticAbbreviationTableBuilder
{
    private readonly List<byte> _bytes = new();

    public int Position
    {
        get { return _bytes.Count; }
    }

    public SyntheticAbbreviationTableBuilder AppendAbbreviation(
        int code,
        DwarfTag tag,
        bool hasChildren,
        IReadOnlyList<SyntheticAbbreviationAttribute> attributes)
    {
        AppendUnsignedLeb128((ulong)code);
        AppendUnsignedLeb128((ulong)tag);
        _bytes.Add(hasChildren ? (byte)1 : (byte)0);
        foreach (SyntheticAbbreviationAttribute attribute in attributes)
        {
            AppendUnsignedLeb128((ulong)attribute.Attribute);
            AppendUnsignedLeb128((ulong)attribute.Form);
            if (attribute.Form == DwarfForm.ImplicitConst)
            {
                AppendSignedLeb128(attribute.ImplicitConstValue);
            }
        }
        AppendUnsignedLeb128(0);
        AppendUnsignedLeb128(0);
        return this;
    }

    public SyntheticAbbreviationTableBuilder AppendTableTerminator()
    {
        AppendUnsignedLeb128(0);
        return this;
    }

    public SyntheticAbbreviationTableBuilder AppendRawByte(byte value)
    {
        _bytes.Add(value);
        return this;
    }

    public SyntheticAbbreviationTableBuilder AppendRawBytes(params byte[] values)
    {
        _bytes.AddRange(values);
        return this;
    }

    public SyntheticAbbreviationTableBuilder AppendUnsignedLeb128Raw(ulong value)
    {
        AppendUnsignedLeb128(value);
        return this;
    }

    public SyntheticAbbreviationTableBuilder AppendSignedLeb128Raw(long value)
    {
        AppendSignedLeb128(value);
        return this;
    }

    public byte[] ToArray()
    {
        return _bytes.ToArray();
    }

    private void AppendUnsignedLeb128(ulong value)
    {
        while (true)
        {
            byte payload = (byte)(value & 0x7F);
            value >>= 7;
            if (value == 0)
            {
                _bytes.Add(payload);
                return;
            }
            _bytes.Add((byte)(payload | 0x80));
        }
    }

    private void AppendSignedLeb128(long value)
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
                return;
            }
            _bytes.Add((byte)(payload | 0x80));
            value = nextValue;
        }
    }
}
