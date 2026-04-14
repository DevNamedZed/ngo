// -----------------------------------------------------------------------
// <copyright file="DwarfForm.cs" company="Ziad">
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
    /// Values for the <c>DW_FORM_*</c> field of an abbreviation
    /// attribute spec. The form determines the exact byte layout of
    /// the attribute's value inside <c>.debug_info</c>, so the
    /// parser must recognise every form a compiler might emit.
    /// Unknown forms are a hard error (the parser cannot know how
    /// many bytes to skip, so silent fall-through would cascade
    /// into nonsense downstream). All DWARF 4 and DWARF 5 standard
    /// forms are represented; vendor-extension forms cast through
    /// as unknown and trigger <see cref="DwarfParseException"/>.
    /// </summary>
    public enum DwarfForm
    {
        Null = 0x00,
        Addr = 0x01,
        Block2 = 0x03,
        Block4 = 0x04,
        Data2 = 0x05,
        Data4 = 0x06,
        Data8 = 0x07,
        String = 0x08,
        Block = 0x09,
        Block1 = 0x0A,
        Data1 = 0x0B,
        Flag = 0x0C,
        Sdata = 0x0D,
        Strp = 0x0E,
        Udata = 0x0F,
        RefAddr = 0x10,
        Ref1 = 0x11,
        Ref2 = 0x12,
        Ref4 = 0x13,
        Ref8 = 0x14,
        RefUdata = 0x15,
        Indirect = 0x16,
        SecOffset = 0x17,
        Exprloc = 0x18,
        FlagPresent = 0x19,
        Strx = 0x1A,
        Addrx = 0x1B,
        RefSup4 = 0x1C,
        StrpSup = 0x1D,
        Data16 = 0x1E,
        LineStrp = 0x1F,
        RefSig8 = 0x20,
        ImplicitConst = 0x21,
        Loclistx = 0x22,
        Rnglistx = 0x23,
        RefSup8 = 0x24,
        Strx1 = 0x25,
        Strx2 = 0x26,
        Strx3 = 0x27,
        Strx4 = 0x28,
        Addrx1 = 0x29,
        Addrx2 = 0x2A,
        Addrx3 = 0x2B,
        Addrx4 = 0x2C,
    }
}
