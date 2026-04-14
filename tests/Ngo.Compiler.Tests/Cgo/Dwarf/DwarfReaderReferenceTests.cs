// -----------------------------------------------------------------------
// <copyright file="DwarfReaderReferenceTests.cs" company="Ziad">
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

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Tests that <see cref="DwarfReader"/> resolves DIE references
/// correctly. <see cref="DwarfForm.Ref1"/>/<see cref="DwarfForm.Ref2"/>/etc.
/// are stored as CU-relative offsets but surface as absolute
/// <c>.debug_info</c> offsets so the Layer-4 type resolver can
/// look a referent up in
/// <see cref="DwarfCompilationUnit.DiesByOffsetInDebugInfo"/>
/// without knowing which form was used. <see cref="DwarfForm.RefAddr"/>
/// is already absolute and passes through unchanged. Cross-CU
/// sanity: a <see cref="DwarfForm.Ref4"/> in the second CU must
/// resolve against its own CU header, not the first.
/// </summary>
[TestClass]
public class DwarfReaderReferenceTests
{
    [TestMethod]
    public void Ref1_ResolvesToAbsoluteOffsetInFirstCu()
    {
        DwarfAttributeValue value = BuildReferenceInFirstCu(
            DwarfForm.Ref1,
            builder => builder.AppendU8(0x15));

        AssertReference(value, DwarfForm.Ref1, expectedOffset: 0x15);
    }

    [TestMethod]
    public void Ref2_ResolvesToAbsoluteOffsetInFirstCu()
    {
        DwarfAttributeValue value = BuildReferenceInFirstCu(
            DwarfForm.Ref2,
            builder => builder.AppendU16(0x2010));

        AssertReference(value, DwarfForm.Ref2, expectedOffset: 0x2010);
    }

    [TestMethod]
    public void Ref4_ResolvesToAbsoluteOffsetInFirstCu()
    {
        DwarfAttributeValue value = BuildReferenceInFirstCu(
            DwarfForm.Ref4,
            builder => builder.AppendU32(0x12345));

        AssertReference(value, DwarfForm.Ref4, expectedOffset: 0x12345);
    }

    [TestMethod]
    public void Ref8_ResolvesToAbsoluteOffsetInFirstCu()
    {
        DwarfAttributeValue value = BuildReferenceInFirstCu(
            DwarfForm.Ref8,
            builder => builder.AppendU64(0x1234));

        AssertReference(value, DwarfForm.Ref8, expectedOffset: 0x1234);
    }

    [TestMethod]
    public void RefUdata_ResolvesToAbsoluteOffsetInFirstCu()
    {
        DwarfAttributeValue value = BuildReferenceInFirstCu(
            DwarfForm.RefUdata,
            builder => builder.AppendUnsignedLeb128(0x321));

        AssertReference(value, DwarfForm.RefUdata, expectedOffset: 0x321);
    }

    [TestMethod]
    public void RefAddr_PassesThroughAsAbsolute()
    {
        DwarfAttributeValue value = BuildReferenceInFirstCu(
            DwarfForm.RefAddr,
            builder => builder.AppendU32(0xABCDE));

        AssertReference(value, DwarfForm.RefAddr, expectedOffset: 0xABCDE);
    }

    [TestMethod]
    public void Ref4_InSecondCu_ResolvesAgainstSecondCuHeader()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Type, DwarfForm.Ref4),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU32(0x50);
        debugInfoBuilder.EndCompilationUnit();
        int secondCuOffset = debugInfoBuilder.Position;
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU32(0x50);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfAttributeValue firstCuReference =
            debugInfo.CompilationUnits[0].TopLevelDies[0].Attributes[DwarfAttribute.Type];
        AssertReference(firstCuReference, DwarfForm.Ref4, expectedOffset: 0x50);

        DwarfAttributeValue secondCuReference =
            debugInfo.CompilationUnits[1].TopLevelDies[0].Attributes[DwarfAttribute.Type];
        AssertReference(
            secondCuReference, DwarfForm.Ref4, expectedOffset: secondCuOffset + 0x50);
    }

    [TestMethod]
    public void Ref4_TargetDie_IsReachableViaDiesByOffset()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>())
            .AppendAbbreviation(
                2,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, DwarfForm.Data1),
                })
            .AppendAbbreviation(
                3,
                DwarfTag.PointerType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Type, DwarfForm.Ref4),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendU8(4);
        int pointerDieCuRelativeTarget = 12;
        debugInfoBuilder.AppendUnsignedLeb128(3);
        debugInfoBuilder.AppendU32((uint)pointerDieCuRelativeTarget);
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        DwarfDie pointerDie = compilationUnit.TopLevelDies[0].Children[1];
        Assert.AreEqual(DwarfTag.PointerType, pointerDie.Tag);
        DwarfReferenceAttributeValue reference =
            (DwarfReferenceAttributeValue)pointerDie.Attributes[DwarfAttribute.Type];
        DwarfDie target = compilationUnit.DiesByOffsetInDebugInfo[reference.OffsetInDebugInfo];
        Assert.AreEqual(DwarfTag.BaseType, target.Tag);
    }

    private static DwarfAttributeValue BuildReferenceInFirstCu(
        DwarfForm form, System.Action<SyntheticDebugInfoBuilder> appendValueBytes)
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Type, form),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        appendValueBytes(debugInfoBuilder);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        return debugInfo.CompilationUnits[0].TopLevelDies[0].Attributes[DwarfAttribute.Type];
    }

    private static void AssertReference(
        DwarfAttributeValue value, DwarfForm expectedForm, int expectedOffset)
    {
        Assert.IsInstanceOfType(value, typeof(DwarfReferenceAttributeValue));
        Assert.AreEqual(expectedForm, value.Form);
        Assert.AreEqual(expectedOffset, ((DwarfReferenceAttributeValue)value).OffsetInDebugInfo);
    }
}
