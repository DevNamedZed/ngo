// -----------------------------------------------------------------------
// <copyright file="DwarfTypeResolverStructLayoutTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.Dwarf;
using Ngo.Compiler.Cgo.Symbols;
using Ngo.Compiler.Tests.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Symbols;

/// <summary>
/// Exercises <see cref="DwarfTypeResolver.ResolveStructLayout"/>.
/// Coverage targets: plain integer-offset members, union members
/// co-located at offset zero, opaque forward declarations, bitfields
/// via DWARF 4+ <c>DW_AT_data_bit_offset</c>, <c>DW_OP_plus_uconst</c>
/// location expressions, and the error paths that make
/// <c>CgoDebugInfoException</c> the single catchable outcome: legacy
/// <c>DW_AT_bit_offset</c> and location expressions that are
/// anything other than a single plus-uconst.
/// </summary>
[TestClass]
public class DwarfTypeResolverStructLayoutTests
{
    [TestMethod]
    public void ResolveStructLayout_SimpleStruct_ReturnsFieldsAtExpectedOffsets()
    {
        SyntheticCompilationUnit unit = BuildPointStruct(
            locationForm: DwarfForm.Udata, legacyBitOffset: false);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("point"));

        Assert.AreEqual("Point", layout.Name);
        Assert.IsFalse(layout.IsUnion);
        Assert.IsFalse(layout.IsOpaque);
        Assert.AreEqual(8L, layout.SizeBytes);
        Assert.AreEqual(2, layout.Fields.Count);

        Assert.AreEqual("x", layout.Fields[0].Name);
        Assert.AreEqual(0L, layout.Fields[0].OffsetBytes);
        Assert.AreEqual(4L, layout.Fields[0].SizeBytes);
        Assert.AreEqual(0, layout.Fields[0].BitOffset);
        Assert.AreEqual(0, layout.Fields[0].BitSize);
        Assert.IsFalse(layout.Fields[0].IsBitfield);

        Assert.AreEqual("y", layout.Fields[1].Name);
        Assert.AreEqual(4L, layout.Fields[1].OffsetBytes);
        Assert.AreEqual(4L, layout.Fields[1].SizeBytes);
    }

    [TestMethod]
    public void ResolveStructLayout_UnionMembers_AllReportOffsetZero()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int unionAbbrev = unit.DeclareAbbreviation(
            DwarfTag.UnionType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int memberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("union");
        unit.AppendAbbreviationCode(unionAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("MyUnion");
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("asInt");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("asFloat");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("union"));

        Assert.IsTrue(layout.IsUnion);
        Assert.AreEqual("MyUnion", layout.Name);
        Assert.AreEqual(2, layout.Fields.Count);
        Assert.AreEqual(0L, layout.Fields[0].OffsetBytes);
        Assert.AreEqual(0L, layout.Fields[1].OffsetBytes);
    }

    [TestMethod]
    public void ResolveStructLayout_OpaqueForwardDeclaration_ReturnsOpaqueLayout()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int opaqueStructAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Declaration, DwarfForm.FlagPresent),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("opaque");
        unit.AppendAbbreviationCode(opaqueStructAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("sqlite3_backup");
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("opaque"));

        Assert.IsTrue(layout.IsOpaque);
        Assert.AreEqual("sqlite3_backup", layout.Name);
        Assert.AreEqual(0L, layout.SizeBytes);
        Assert.AreEqual(0, layout.Fields.Count);
    }

    [TestMethod]
    public void ResolveStructLayout_PlusUconstLocation_DecodesOffset()
    {
        SyntheticCompilationUnit unit = BuildPointStruct(
            locationForm: DwarfForm.Exprloc, legacyBitOffset: false);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("point"));

        Assert.AreEqual(0L, layout.Fields[0].OffsetBytes);
        Assert.AreEqual(4L, layout.Fields[1].OffsetBytes);
    }

    [TestMethod]
    public void ResolveStructLayout_LocationExpressionWithUnsupportedOpcode_Throws()
    {
        SyntheticCompilationUnit unit = BuildStructWithSingleFieldExpression(
            expressionBytes: new byte[] { 0x22 });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("struct")));
        StringAssert.Contains(exception.Message, "DW_OP_plus_uconst");
    }

    [TestMethod]
    public void ResolveStructLayout_LocationExpressionWithTrailingBytes_Throws()
    {
        SyntheticCompilationUnit unit = BuildStructWithSingleFieldExpression(
            expressionBytes: new byte[] { 0x23, 0x04, 0x00 });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("struct")));
        StringAssert.Contains(exception.Message, "trailing");
    }

    [TestMethod]
    public void ResolveStructLayout_EmptyLocationExpression_Throws()
    {
        SyntheticCompilationUnit unit = BuildStructWithSingleFieldExpression(
            expressionBytes: System.Array.Empty<byte>());
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("struct")));
        StringAssert.Contains(exception.Message, "empty");
    }

    [TestMethod]
    public void ResolveStructLayout_LegacyBitOffset_Throws()
    {
        SyntheticCompilationUnit unit = BuildPointStruct(
            locationForm: DwarfForm.Udata, legacyBitOffset: true);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("point")));
        StringAssert.Contains(exception.Message, "DW_AT_bit_offset");
    }

    [TestMethod]
    public void ResolveStructLayout_WithBitfield_ReportsBitOffsetAndSize()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int bitfieldMemberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
                new(DwarfAttribute.BitSize, DwarfForm.Data1),
                new(DwarfAttribute.DataBitOffset, DwarfForm.Udata),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("Flags");
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(bitfieldMemberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("isEnabled");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.DebugInfoBuilder.AppendU8(1);
        unit.DebugInfoBuilder.AppendUnsignedLeb128(3);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(bitfieldMemberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("mode");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.DebugInfoBuilder.AppendU8(5);
        unit.DebugInfoBuilder.AppendUnsignedLeb128(10);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("struct"));

        Assert.AreEqual(2, layout.Fields.Count);

        DwarfResolvedField isEnabled = layout.Fields[0];
        Assert.IsTrue(isEnabled.IsBitfield);
        Assert.AreEqual(0L, isEnabled.OffsetBytes);
        Assert.AreEqual(3, isEnabled.BitOffset);
        Assert.AreEqual(1, isEnabled.BitSize);

        DwarfResolvedField mode = layout.Fields[1];
        Assert.IsTrue(mode.IsBitfield);
        Assert.AreEqual(1L, mode.OffsetBytes);
        Assert.AreEqual(2, mode.BitOffset);
        Assert.AreEqual(5, mode.BitSize);
    }

    [TestMethod]
    public void ResolveStructLayout_FieldWithTypedef_UnwrapsToBaseType()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int typedefAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Typedef,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int memberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
                new(DwarfAttribute.DataMemberLocation, DwarfForm.Udata),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("typedef");
        unit.AppendAbbreviationCode(typedefAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("value");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("typedef"));
        unit.DebugInfoBuilder.AppendUnsignedLeb128(0);
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("struct"));

        Assert.AreEqual(1, layout.Fields.Count);
        Assert.AreEqual(DwarfTag.BaseType, layout.Fields[0].TypeDie.Tag);
        Assert.AreEqual(unit.GetDieOffset("baseType"), layout.Fields[0].TypeDie.OffsetInDebugInfo);
    }

    [TestMethod]
    public void ResolveStructLayout_MemberWithoutLocation_DefaultsToOffsetZero()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int locationlessMemberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(locationlessMemberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("first");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedStructLayout layout =
            resolver.ResolveStructLayout(unit.GetDie("struct"));

        Assert.AreEqual(1, layout.Fields.Count);
        Assert.AreEqual(0L, layout.Fields[0].OffsetBytes);
    }

    [TestMethod]
    public void ResolveStructLayout_StructMissingByteSize_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>());
        int memberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("x");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("struct")));
        StringAssert.Contains(exception.Message, "DW_AT_byte_size");
    }

    [TestMethod]
    public void ResolveStructLayout_MemberMissingName_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int namelessMemberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(namelessMemberAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("struct")));
        StringAssert.Contains(exception.Message, "DW_AT_name");
    }

    [TestMethod]
    public void ResolveStructLayout_OnBaseTypeTag_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveStructLayout(unit.GetDie("baseType")));
        StringAssert.Contains(exception.Message, "StructureType");
    }

    private static SyntheticCompilationUnit BuildPointStruct(
        DwarfForm locationForm, bool legacyBitOffset)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });

        List<SyntheticAbbreviationAttribute> memberAttributes = new()
        {
            new(DwarfAttribute.Name, DwarfForm.String),
            new(DwarfAttribute.Type, DwarfForm.Ref4),
            new(DwarfAttribute.DataMemberLocation, locationForm),
        };
        if (legacyBitOffset)
        {
            memberAttributes.Add(new(DwarfAttribute.BitOffset, DwarfForm.Data1));
        }
        int memberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member, hasChildren: false, memberAttributes);

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("point");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("Point");
        unit.DebugInfoBuilder.AppendU8(8);

        AppendMemberDie(unit, memberAbbrev, "x", unit.GetDieOffset("baseType"), locationForm, 0, legacyBitOffset);
        AppendMemberDie(unit, memberAbbrev, "y", unit.GetDieOffset("baseType"), locationForm, 4, legacyBitOffset);

        unit.AppendNullDie();
        unit.AppendNullDie();

        return unit;
    }

    private static void AppendMemberDie(
        SyntheticCompilationUnit unit,
        int memberAbbrev,
        string name,
        int typeOffset,
        DwarfForm locationForm,
        long offset,
        bool legacyBitOffset)
    {
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(name);
        unit.DebugInfoBuilder.AppendU32((uint)typeOffset);
        if (locationForm == DwarfForm.Udata)
        {
            unit.DebugInfoBuilder.AppendUnsignedLeb128((ulong)offset);
        }
        else if (locationForm == DwarfForm.Exprloc)
        {
            List<byte> expressionBytes = new() { 0x23 };
            ulong value = (ulong)offset;
            while (true)
            {
                byte payload = (byte)(value & 0x7F);
                value >>= 7;
                if (value == 0)
                {
                    expressionBytes.Add(payload);
                    break;
                }
                expressionBytes.Add((byte)(payload | 0x80));
            }
            unit.DebugInfoBuilder.AppendUnsignedLeb128((ulong)expressionBytes.Count);
            unit.DebugInfoBuilder.AppendRawBytes(expressionBytes.ToArray());
        }
        else
        {
            throw new System.ArgumentOutOfRangeException(nameof(locationForm));
        }
        if (legacyBitOffset)
        {
            unit.DebugInfoBuilder.AppendU8(0);
        }
    }

    private static SyntheticCompilationUnit BuildStructWithSingleFieldExpression(
        byte[] expressionBytes)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int memberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
                new(DwarfAttribute.DataMemberLocation, DwarfForm.Exprloc),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("field");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.DebugInfoBuilder.AppendUnsignedLeb128((ulong)expressionBytes.Length);
        unit.DebugInfoBuilder.AppendRawBytes(expressionBytes);
        unit.AppendNullDie();
        unit.AppendNullDie();

        return unit;
    }
}
