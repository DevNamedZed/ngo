// -----------------------------------------------------------------------
// <copyright file="DwarfTag.cs" company="Ziad">
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
    /// Values for the <c>DW_TAG_*</c> field of a DIE abbreviation
    /// entry. Only the standard DWARF 4 and DWARF 5 tag codes are
    /// named; vendor-extension codes are still representable because
    /// the underlying type is <see cref="int"/> and casts from an
    /// unknown numeric code are valid. The classification in Layer 4
    /// dispatches on the named values and preserves the numeric code
    /// in diagnostics for anything it does not recognise.
    /// </summary>
    public enum DwarfTag
    {
        Null = 0x00,
        ArrayType = 0x01,
        ClassType = 0x02,
        EntryPoint = 0x03,
        EnumerationType = 0x04,
        FormalParameter = 0x05,
        ImportedDeclaration = 0x08,
        Label = 0x0A,
        LexicalBlock = 0x0B,
        Member = 0x0D,
        PointerType = 0x0F,
        ReferenceType = 0x10,
        CompileUnit = 0x11,
        StringType = 0x12,
        StructureType = 0x13,
        SubroutineType = 0x15,
        Typedef = 0x16,
        UnionType = 0x17,
        UnspecifiedParameters = 0x18,
        Variant = 0x19,
        CommonBlock = 0x1A,
        CommonInclusion = 0x1B,
        Inheritance = 0x1C,
        InlinedSubroutine = 0x1D,
        Module = 0x1E,
        PointerToMemberType = 0x1F,
        SetType = 0x20,
        SubrangeType = 0x21,
        WithStmt = 0x22,
        AccessDeclaration = 0x23,
        BaseType = 0x24,
        CatchBlock = 0x25,
        ConstType = 0x26,
        Constant = 0x27,
        Enumerator = 0x28,
        FileType = 0x29,
        Friend = 0x2A,
        Namelist = 0x2B,
        NamelistItem = 0x2C,
        PackedType = 0x2D,
        Subprogram = 0x2E,
        TemplateTypeParameter = 0x2F,
        TemplateValueParameter = 0x30,
        ThrownType = 0x31,
        TryBlock = 0x32,
        VariantPart = 0x33,
        Variable = 0x34,
        VolatileType = 0x35,
        DwarfProcedure = 0x36,
        RestrictType = 0x37,
        InterfaceType = 0x38,
        Namespace = 0x39,
        ImportedModule = 0x3A,
        UnspecifiedType = 0x3B,
        PartialUnit = 0x3C,
        ImportedUnit = 0x3D,
        Condition = 0x3F,
        SharedType = 0x40,
        TypeUnit = 0x41,
        RvalueReferenceType = 0x42,
        TemplateAlias = 0x43,
        CoarrayType = 0x44,
        GenericSubrange = 0x45,
        DynamicType = 0x46,
        AtomicType = 0x47,
        CallSite = 0x48,
        CallSiteParameter = 0x49,
        SkeletonUnit = 0x4A,
        ImmutableType = 0x4B,
    }
}
