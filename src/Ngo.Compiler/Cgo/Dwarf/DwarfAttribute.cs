// -----------------------------------------------------------------------
// <copyright file="DwarfAttribute.cs" company="Ziad">
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
    /// Values for the <c>DW_AT_*</c> attribute field of a DIE
    /// abbreviation entry. Named values cover the standard DWARF 4
    /// and DWARF 5 attributes that the cgo symbol source consults
    /// and any common neighbour attributes present in debug info
    /// emitted by gcc and clang. Vendor extensions remain usable
    /// via numeric cast.
    /// </summary>
    public enum DwarfAttribute
    {
        Null = 0x00,
        Sibling = 0x01,
        Location = 0x02,
        Name = 0x03,
        Ordering = 0x09,
        ByteSize = 0x0B,
        BitOffset = 0x0C,
        BitSize = 0x0D,
        StmtList = 0x10,
        LowPc = 0x11,
        HighPc = 0x12,
        Language = 0x13,
        Discr = 0x15,
        DiscrValue = 0x16,
        Visibility = 0x17,
        Import = 0x18,
        StringLength = 0x19,
        CommonReference = 0x1A,
        CompDir = 0x1B,
        ConstValue = 0x1C,
        ContainingType = 0x1D,
        DefaultValue = 0x1E,
        Inline = 0x20,
        IsOptional = 0x21,
        LowerBound = 0x22,
        Producer = 0x25,
        Prototyped = 0x27,
        ReturnAddr = 0x2A,
        StartScope = 0x2C,
        BitStride = 0x2E,
        UpperBound = 0x2F,
        AbstractOrigin = 0x31,
        Accessibility = 0x32,
        AddressClass = 0x33,
        Artificial = 0x34,
        BaseTypes = 0x35,
        CallingConvention = 0x36,
        Count = 0x37,
        DataMemberLocation = 0x38,
        DeclColumn = 0x39,
        DeclFile = 0x3A,
        DeclLine = 0x3B,
        Declaration = 0x3C,
        DiscrList = 0x3D,
        Encoding = 0x3E,
        External = 0x3F,
        FrameBase = 0x40,
        Friend = 0x41,
        IdentifierCase = 0x42,
        MacroInfo = 0x43,
        NamelistItem = 0x44,
        Priority = 0x45,
        Segment = 0x46,
        Specification = 0x47,
        StaticLink = 0x48,
        Type = 0x49,
        UseLocation = 0x4A,
        VariableParameter = 0x4B,
        Virtuality = 0x4C,
        VtableElemLocation = 0x4D,
        Allocated = 0x4E,
        Associated = 0x4F,
        DataLocation = 0x50,
        ByteStride = 0x51,
        EntryPc = 0x52,
        UseUtf8 = 0x53,
        Extension = 0x54,
        Ranges = 0x55,
        Trampoline = 0x56,
        CallColumn = 0x57,
        CallFile = 0x58,
        CallLine = 0x59,
        Description = 0x5A,
        BinaryScale = 0x5B,
        DecimalScale = 0x5C,
        Small = 0x5D,
        DecimalSign = 0x5E,
        DigitCount = 0x5F,
        PictureString = 0x60,
        Mutable = 0x61,
        ThreadsScaled = 0x62,
        Explicit = 0x63,
        ObjectPointer = 0x64,
        Endianity = 0x65,
        Elemental = 0x66,
        Pure = 0x67,
        Recursive = 0x68,
        Signature = 0x69,
        MainSubprogram = 0x6A,
        DataBitOffset = 0x6B,
        ConstExpr = 0x6C,
        EnumClass = 0x6D,
        LinkageName = 0x6E,
        StringLengthBitSize = 0x6F,
        StringLengthByteSize = 0x70,
        Rank = 0x71,
        StrOffsetsBase = 0x72,
        AddrBase = 0x73,
        RnglistsBase = 0x74,
        DwoName = 0x76,
        Reference = 0x77,
        RvalueReference = 0x78,
        Macros = 0x79,
        CallAllCalls = 0x7A,
        CallAllSourceCalls = 0x7B,
        CallAllTailCalls = 0x7C,
        CallReturnPc = 0x7D,
        CallValue = 0x7E,
        CallOrigin = 0x7F,
        CallParameter = 0x80,
        CallPc = 0x81,
        CallTailCall = 0x82,
        CallTarget = 0x83,
        CallTargetClobbered = 0x84,
        CallDataLocation = 0x85,
        CallDataValue = 0x86,
        NoReturn = 0x87,
        Alignment = 0x88,
        ExportSymbols = 0x89,
        Deleted = 0x8A,
        Defaulted = 0x8B,
        LoclistsBase = 0x8C,
    }
}
