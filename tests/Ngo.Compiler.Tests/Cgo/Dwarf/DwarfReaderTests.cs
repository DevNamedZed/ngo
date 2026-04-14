// -----------------------------------------------------------------------
// <copyright file="DwarfReaderTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Tests for <see cref="DwarfReader"/> covering CU header parsing,
/// DIE tree shape, and cross-CU structure. Every byte array is
/// laid out by <see cref="SyntheticDebugInfoBuilder"/> so a test
/// case reads as "build this CU, parse it, check the tree" rather
/// than "count bytes". Form-decoding corner cases live in
/// <see cref="DwarfReaderFormTests"/>; error paths live in
/// <see cref="DwarfReaderErrorTests"/>.
/// </summary>
[TestClass]
public class DwarfReaderTests
{
    [TestMethod]
    public void Read_EmptyDebugInfo_ReturnsZeroCompilationUnits()
    {
        DwarfSections sections = new(
            Array.Empty<byte>(),
            new byte[] { 0x00 },
            null,
            null);

        DwarfDebugInfo debugInfo = DwarfReader.Read(sections);

        Assert.AreEqual(0, debugInfo.CompilationUnits.Count);
        Assert.AreEqual(DwarfFormat.Dwarf4, debugInfo.Format);
    }

    [TestMethod]
    public void Read_NullSections_ThrowsArgumentNull()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => DwarfReader.Read(null!));
    }

    [TestMethod]
    public void Read_SingleDwarf4Dwarf32CU_SetsHeaderFields()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Language, DwarfForm.Data1),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("hello.c");
        debugInfoBuilder.AppendU8((byte)DwarfLanguage.C99);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        Assert.AreEqual(1, debugInfo.CompilationUnits.Count);
        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.AreEqual(4, compilationUnit.Version);
        Assert.AreEqual(8, compilationUnit.AddressSize);
        Assert.AreEqual(DwarfUnitFormat.Dwarf32, compilationUnit.UnitFormat);
        Assert.AreEqual(0, compilationUnit.HeaderOffsetInDebugInfo);
        Assert.AreEqual(0, compilationUnit.DebugAbbrevOffset);
        Assert.AreEqual("hello.c", compilationUnit.Name);
        Assert.AreEqual(DwarfLanguage.C99, compilationUnit.Language);
    }

    [TestMethod]
    public void Read_SingleDwarf5CU_SetsVersion5()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf5, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("hello5.c");
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        Assert.AreEqual(1, debugInfo.CompilationUnits.Count);
        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.AreEqual(5, compilationUnit.Version);
        Assert.AreEqual(DwarfFormat.Dwarf5, debugInfo.Format);
    }

    [TestMethod]
    public void Read_Dwarf64CU_UsesEightByteOffsets()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf64, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("hello64.c");
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        Assert.AreEqual(DwarfUnitFormat.Dwarf64, debugInfo.CompilationUnits[0].UnitFormat);
        Assert.AreEqual("hello64.c", debugInfo.CompilationUnits[0].Name);
    }

    [TestMethod]
    public void Read_TwoCompilationUnits_ParsesBoth()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("first.c");
        debugInfoBuilder.EndCompilationUnit();
        int secondCuOffset = debugInfoBuilder.Position;
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("second.c");
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        Assert.AreEqual(2, debugInfo.CompilationUnits.Count);
        Assert.AreEqual("first.c", debugInfo.CompilationUnits[0].Name);
        Assert.AreEqual(0, debugInfo.CompilationUnits[0].HeaderOffsetInDebugInfo);
        Assert.AreEqual("second.c", debugInfo.CompilationUnits[1].Name);
        Assert.AreEqual(secondCuOffset, debugInfo.CompilationUnits[1].HeaderOffsetInDebugInfo);
    }

    [TestMethod]
    public void Read_CompilationUnitWithChildren_BuildsTree()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendAbbreviation(
                2,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, DwarfForm.Data1),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("tree.c");
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendNullTerminatedUtf8("int");
        debugInfoBuilder.AppendU8(4);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendNullTerminatedUtf8("short");
        debugInfoBuilder.AppendU8(2);
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.AreEqual(1, compilationUnit.TopLevelDies.Count);
        DwarfDie compileUnitDie = compilationUnit.TopLevelDies[0];
        Assert.AreEqual(DwarfTag.CompileUnit, compileUnitDie.Tag);
        Assert.AreEqual(2, compileUnitDie.Children.Count);
        Assert.AreEqual(DwarfTag.BaseType, compileUnitDie.Children[0].Tag);
        Assert.AreEqual("int", compileUnitDie.Children[0].Attributes[DwarfAttribute.Name].AsString());
        Assert.AreEqual(4, compileUnitDie.Children[0].Attributes[DwarfAttribute.ByteSize].AsInteger());
        Assert.AreEqual("short", compileUnitDie.Children[1].Attributes[DwarfAttribute.Name].AsString());
        Assert.AreEqual(2, compileUnitDie.Children[1].Attributes[DwarfAttribute.ByteSize].AsInteger());
    }

    [TestMethod]
    public void Read_NestedChildren_BuildsDeepTree()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>())
            .AppendAbbreviation(
                2,
                DwarfTag.StructureType,
                true,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendAbbreviation(
                3,
                DwarfTag.Member,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendNullTerminatedUtf8("point");
        debugInfoBuilder.AppendUnsignedLeb128(3);
        debugInfoBuilder.AppendNullTerminatedUtf8("x");
        debugInfoBuilder.AppendUnsignedLeb128(3);
        debugInfoBuilder.AppendNullTerminatedUtf8("y");
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfDie cu = debugInfo.CompilationUnits[0].TopLevelDies[0];
        Assert.AreEqual(DwarfTag.CompileUnit, cu.Tag);
        Assert.AreEqual(1, cu.Children.Count);
        DwarfDie structure = cu.Children[0];
        Assert.AreEqual(DwarfTag.StructureType, structure.Tag);
        Assert.AreEqual("point", structure.Attributes[DwarfAttribute.Name].AsString());
        Assert.AreEqual(2, structure.Children.Count);
        Assert.AreEqual("x", structure.Children[0].Attributes[DwarfAttribute.Name].AsString());
        Assert.AreEqual("y", structure.Children[1].Attributes[DwarfAttribute.Name].AsString());
    }

    [TestMethod]
    public void Read_DiesByOffsetInDebugInfo_IndexesEveryDie()
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
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.AreEqual(3, compilationUnit.DiesByOffsetInDebugInfo.Count);
        DwarfDie cu = compilationUnit.TopLevelDies[0];
        Assert.IsTrue(compilationUnit.DiesByOffsetInDebugInfo.ContainsKey(cu.OffsetInDebugInfo));
        Assert.IsTrue(
            compilationUnit.DiesByOffsetInDebugInfo.ContainsKey(cu.Children[0].OffsetInDebugInfo));
        Assert.IsTrue(
            compilationUnit.DiesByOffsetInDebugInfo.ContainsKey(cu.Children[1].OffsetInDebugInfo));
    }

    [TestMethod]
    public void Read_TopLevelDies_ContainsOnlyCuDie()
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
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendUnsignedLeb128(2);
        debugInfoBuilder.AppendUnsignedLeb128(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.AreEqual(1, compilationUnit.TopLevelDies.Count);
        Assert.AreEqual(DwarfTag.CompileUnit, compilationUnit.TopLevelDies[0].Tag);
    }

    [TestMethod]
    public void Read_AttributesByEnum_AreAccessibleByTag()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.CompDir, DwarfForm.String),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Producer, DwarfForm.String),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Language, DwarfForm.Data1),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("main.c");
        debugInfoBuilder.AppendNullTerminatedUtf8("/src");
        debugInfoBuilder.AppendNullTerminatedUtf8("clang 18");
        debugInfoBuilder.AppendU8((byte)DwarfLanguage.C11);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.AreEqual("main.c", compilationUnit.Name);
        Assert.AreEqual("/src", compilationUnit.CompDir);
        Assert.AreEqual("clang 18", compilationUnit.Producer);
        Assert.AreEqual(DwarfLanguage.C11, compilationUnit.Language);
    }

    [TestMethod]
    public void Read_CuWithoutOptionalAttributes_LeavesThemNull()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfCompilationUnit compilationUnit = debugInfo.CompilationUnits[0];
        Assert.IsNull(compilationUnit.Name);
        Assert.IsNull(compilationUnit.CompDir);
        Assert.IsNull(compilationUnit.Producer);
        Assert.AreEqual(DwarfLanguage.Unknown, compilationUnit.Language);
    }

    [TestMethod]
    public void Read_MultipleCuShareAbbrevTable_ParsesBoth()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.String),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("alpha.c");
        debugInfoBuilder.EndCompilationUnit();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendNullTerminatedUtf8("beta.c");
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        Assert.AreEqual(2, debugInfo.CompilationUnits.Count);
        Assert.AreEqual("alpha.c", debugInfo.CompilationUnits[0].Name);
        Assert.AreEqual("beta.c", debugInfo.CompilationUnits[1].Name);
    }
}
