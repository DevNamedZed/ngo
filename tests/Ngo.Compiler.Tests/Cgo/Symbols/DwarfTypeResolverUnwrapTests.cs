// -----------------------------------------------------------------------
// <copyright file="DwarfTypeResolverUnwrapTests.cs" company="Ziad">
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
/// Exercises <see cref="DwarfTypeResolver.UnwrapTypeAliases"/> and
/// <see cref="DwarfTypeResolver.ResolveTypeReference"/>. Every C
/// qualifier that carries no layout meaning (typedef, const,
/// volatile, restrict, atomic) must be transparently stripped so
/// downstream consumers see the real underlying type. Tags that
/// are layout-carrying (struct, union, enum, base type, pointer)
/// must pass through unchanged.
/// </summary>
[TestClass]
public class DwarfTypeResolverUnwrapTests
{
    [TestMethod]
    public void UnwrapTypeAliases_OnBaseType_ReturnsSameDie()
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
                new(DwarfAttribute.Encoding, DwarfForm.Data1),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.DebugInfoBuilder.AppendU8((byte)DwarfTypeEncoding.Signed);
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);
        DwarfDie baseTypeDie = unit.GetDie("baseType");

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(baseTypeDie);

        Assert.AreSame(baseTypeDie, unwrapped);
    }

    [TestMethod]
    public void UnwrapTypeAliases_OnTypedef_SkipsToUnderlyingType()
    {
        SyntheticCompilationUnit unit = BuildAliasOverBaseType(DwarfTag.Typedef);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie aliasDie = unit.GetDie("alias");
        DwarfDie unwrapped = resolver.UnwrapTypeAliases(aliasDie);

        Assert.AreEqual(DwarfTag.BaseType, unwrapped.Tag);
        Assert.AreEqual(unit.GetDieOffset("baseType"), unwrapped.OffsetInDebugInfo);
    }

    [TestMethod]
    public void UnwrapTypeAliases_OnConstType_SkipsToUnderlyingType()
    {
        SyntheticCompilationUnit unit = BuildAliasOverBaseType(DwarfTag.ConstType);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(unit.GetDie("alias"));

        Assert.AreEqual(DwarfTag.BaseType, unwrapped.Tag);
    }

    [TestMethod]
    public void UnwrapTypeAliases_OnVolatileType_SkipsToUnderlyingType()
    {
        SyntheticCompilationUnit unit = BuildAliasOverBaseType(DwarfTag.VolatileType);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(unit.GetDie("alias"));

        Assert.AreEqual(DwarfTag.BaseType, unwrapped.Tag);
    }

    [TestMethod]
    public void UnwrapTypeAliases_OnRestrictType_SkipsToUnderlyingType()
    {
        SyntheticCompilationUnit unit = BuildAliasOverBaseType(DwarfTag.RestrictType);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(unit.GetDie("alias"));

        Assert.AreEqual(DwarfTag.BaseType, unwrapped.Tag);
    }

    [TestMethod]
    public void UnwrapTypeAliases_OnAtomicType_SkipsToUnderlyingType()
    {
        SyntheticCompilationUnit unit = BuildAliasOverBaseType(DwarfTag.AtomicType);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(unit.GetDie("alias"));

        Assert.AreEqual(DwarfTag.BaseType, unwrapped.Tag);
    }

    [TestMethod]
    public void UnwrapTypeAliases_OnPointerType_DoesNotUnwrap()
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
        int pointerTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.PointerType,
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
        unit.LabelNextDie("pointer");
        unit.AppendAbbreviationCode(pointerTypeAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);
        DwarfDie pointerDie = unit.GetDie("pointer");

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(pointerDie);

        Assert.AreSame(pointerDie, unwrapped);
        Assert.AreEqual(DwarfTag.PointerType, unwrapped.Tag);
    }

    [TestMethod]
    public void UnwrapTypeAliases_ChainOfMultipleAliases_ReachesBaseType()
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
        int aliasAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Typedef,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int constAbbrev = unit.DeclareAbbreviation(
            DwarfTag.ConstType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int volatileAbbrev = unit.DeclareAbbreviation(
            DwarfTag.VolatileType,
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
        unit.LabelNextDie("typedef");
        unit.AppendAbbreviationCode(aliasAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.LabelNextDie("constOverTypedef");
        unit.AppendAbbreviationCode(constAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("typedef"));
        unit.LabelNextDie("volatileOverConst");
        unit.AppendAbbreviationCode(volatileAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("constOverTypedef"));
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie unwrapped = resolver.UnwrapTypeAliases(unit.GetDie("volatileOverConst"));

        Assert.AreEqual(DwarfTag.BaseType, unwrapped.Tag);
        Assert.AreEqual(unit.GetDieOffset("baseType"), unwrapped.OffsetInDebugInfo);
    }

    [TestMethod]
    public void UnwrapTypeAliases_TypedefWithoutType_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int typedefAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Typedef,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>());

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("typedef");
        unit.AppendAbbreviationCode(typedefAbbrev);
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);
        DwarfDie typedefDie = unit.GetDie("typedef");

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.UnwrapTypeAliases(typedefDie));
        StringAssert.Contains(exception.Message, "DW_AT_type");
    }

    [TestMethod]
    public void UnwrapTypeAliases_CyclicTypedef_Throws()
    {
        SyntheticAbbreviationTableBuilder abbreviationBuilder = new();
        abbreviationBuilder.AppendAbbreviation(
            1,
            DwarfTag.CompileUnit,
            true,
            new List<SyntheticAbbreviationAttribute>());
        abbreviationBuilder.AppendAbbreviation(
            2,
            DwarfTag.Typedef,
            false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        abbreviationBuilder.AppendTableTerminator();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        int typedefAOffset = debugInfoBuilder.Position;
        debugInfoBuilder.AppendUnsignedLeb128(2);
        int typedefAReferencePosition = debugInfoBuilder.Position;
        debugInfoBuilder.AppendU32(0);
        int typedefBOffset = debugInfoBuilder.Position;
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendU32((uint)typedefAOffset);
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.EndCompilationUnit();

        byte[] debugInfoBytes = debugInfoBuilder.ToArray();
        uint typedefBOffsetBytes = (uint)typedefBOffset;
        debugInfoBytes[typedefAReferencePosition + 0] = (byte)(typedefBOffsetBytes & 0xFF);
        debugInfoBytes[typedefAReferencePosition + 1] = (byte)((typedefBOffsetBytes >> 8) & 0xFF);
        debugInfoBytes[typedefAReferencePosition + 2] = (byte)((typedefBOffsetBytes >> 16) & 0xFF);
        debugInfoBytes[typedefAReferencePosition + 3] = (byte)((typedefBOffsetBytes >> 24) & 0xFF);

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBytes, abbreviationBuilder.ToArray(), null, null));
        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        DwarfTypeResolver resolver = new(compilationUnit);
        DwarfDie typedefADie = compilationUnit.DiesByOffsetInDebugInfo[typedefAOffset];

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.UnwrapTypeAliases(typedefADie));
        StringAssert.Contains(exception.Message, "cyclic");
    }

    [TestMethod]
    public void ResolveTypeReference_ReturnsReferencedDie()
    {
        SyntheticCompilationUnit unit = BuildAliasOverBaseType(DwarfTag.Typedef);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfDie referenced = resolver.ResolveTypeReference(unit.GetDie("alias"));

        Assert.AreEqual(DwarfTag.BaseType, referenced.Tag);
        Assert.AreEqual(unit.GetDieOffset("baseType"), referenced.OffsetInDebugInfo);
    }

    [TestMethod]
    public void ResolveTypeReference_WithMissingType_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int pointerAbbrev = unit.DeclareAbbreviation(
            DwarfTag.PointerType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>());

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("pointer");
        unit.AppendAbbreviationCode(pointerAbbrev);
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveTypeReference(unit.GetDie("pointer")));
        StringAssert.Contains(exception.Message, "DW_AT_type");
    }

    private static SyntheticCompilationUnit BuildAliasOverBaseType(DwarfTag aliasTag)
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
        int aliasAbbrev = unit.DeclareAbbreviation(
            aliasTag,
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
        unit.LabelNextDie("alias");
        unit.AppendAbbreviationCode(aliasAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();

        return unit;
    }
}
