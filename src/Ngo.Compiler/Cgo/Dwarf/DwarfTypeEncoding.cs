// -----------------------------------------------------------------------
// <copyright file="DwarfTypeEncoding.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// Values for <c>DW_AT_encoding</c> on a
    /// <see cref="DwarfTag.BaseType"/> DIE. Used by the Layer-4 type
    /// classifier to pick the right marshalled .NET primitive —
    /// <c>signed int</c> versus <c>unsigned int</c>, <c>float</c>
    /// versus <c>double</c>, UTF-8 char versus plain signed char.
    /// Named entries cover every encoding DWARF 4 and DWARF 5 define;
    /// vendor extensions stay representable via numeric cast.
    /// </summary>
    public enum DwarfTypeEncoding
    {
        Unknown = 0x00,
        Address = 0x01,
        Boolean = 0x02,
        ComplexFloat = 0x03,
        Float = 0x04,
        Signed = 0x05,
        SignedChar = 0x06,
        Unsigned = 0x07,
        UnsignedChar = 0x08,
        ImaginaryFloat = 0x09,
        PackedDecimal = 0x0A,
        NumericString = 0x0B,
        Edited = 0x0C,
        SignedFixed = 0x0D,
        UnsignedFixed = 0x0E,
        DecimalFloat = 0x0F,
        Utf = 0x10,
        Ucs = 0x11,
        Ascii = 0x12,
    }
}
