// -----------------------------------------------------------------------
// <copyright file="CgoDwarfSymbolSourcePopulateTests.cs" company="Ziad">
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
using Ngo.Compiler.Cgo;
using Ngo.Compiler.Cgo.Dwarf;
using Ngo.Compiler.Cgo.Symbols;
using Ngo.Compiler.Tests.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Symbols;

/// <summary>
/// Unit tests for the DIE-to-catalog classification inside
/// <see cref="CgoDwarfSymbolSource.PopulateFromCompilationUnit"/>.
/// These tests drive the classifier from synthetic DWARF compilation
/// units so the typedef-to-opaque behaviour is pinned independently
/// from the C compiler and module cache that the integration tests
/// in <see cref="CgoDwarfSymbolSourceTests"/> depend on.
///
/// The real-world shape these tests reproduce: library handle types
/// like <c>ZSTD_CCtx</c> and <c>sqlite3</c> are exposed through a
/// typedef alias for an opaque forward-declared struct. The Go side
/// writes <c>C.ZSTD_CCtx</c>, so the classifier must register the
/// typedef name in <see cref="CgoSymbolCatalog.OpaqueTypes"/> —
/// not just the struct tag — otherwise the P/Invoke emitter cannot
/// marshal the handle.
/// </summary>
[TestClass]
public class CgoDwarfSymbolSourcePopulateTests
{
    [TestMethod]
    public void PopulateFromCompilationUnit_TypedefToOpaqueStructWithDifferentName_RegistersBothNamesAsOpaque()
    {
        SyntheticCompilationUnit unit = BuildOpaqueTypedefScenario(
            structTagName: "ZSTD_CCtx_s",
            typedefName: "ZSTD_CCtx");

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(
            catalog.OpaqueTypes.ContainsKey("ZSTD_CCtx_s"),
            "The struct tag must land in OpaqueTypes via the plain struct classifier.");
        Assert.IsTrue(
            catalog.OpaqueTypes.ContainsKey("ZSTD_CCtx"),
            "The typedef name must land in OpaqueTypes so Go-side C.ZSTD_CCtx resolves to the opaque handle.");
        Assert.IsTrue(
            catalog.Typedefs.ContainsKey("ZSTD_CCtx"),
            "The typedef itself must still be registered so the formatter can read the alias C type when needed.");
        Assert.IsFalse(
            catalog.StructsAndUnions.ContainsKey("ZSTD_CCtx_s"),
            "An opaque struct must not appear in StructsAndUnions; RegisterStructOrUnion short-circuits for IsOpaque.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_TypedefToOpaqueStructWithSameName_KeepsSingleOpaqueEntry()
    {
        SyntheticCompilationUnit unit = BuildOpaqueTypedefScenario(
            structTagName: "sqlite3",
            typedefName: "sqlite3");

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(
            catalog.OpaqueTypes.ContainsKey("sqlite3"),
            "When struct tag and typedef name match, a single OpaqueTypes entry covers both.");
        Assert.IsTrue(
            catalog.Typedefs.ContainsKey("sqlite3"),
            "The typedef must still be registered even when the struct tag already populated OpaqueTypes.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_TypedefToConcreteStruct_DoesNotRegisterOpaque()
    {
        SyntheticCompilationUnit unit = BuildConcreteTypedefScenario(
            structTagName: "timespec_s",
            typedefName: "timespec");

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsFalse(
            catalog.OpaqueTypes.ContainsKey("timespec"),
            "A typedef that unwraps to a struct with real fields must not be recorded as opaque.");
        Assert.IsFalse(
            catalog.OpaqueTypes.ContainsKey("timespec_s"),
            "A concrete struct must not end up in OpaqueTypes just because a typedef references it.");
        Assert.IsTrue(
            catalog.StructsAndUnions.ContainsKey("timespec_s"),
            "A concrete struct must be registered in StructsAndUnions.");
        Assert.IsTrue(
            catalog.Typedefs.ContainsKey("timespec"),
            "The typedef itself must be registered with its formatted alias C type.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_TypedefToOpaqueUnion_RegistersTypedefAsOpaque()
    {
        SyntheticCompilationUnit unit = BuildOpaqueTypedefScenario(
            structTagName: "some_union_s",
            typedefName: "some_union",
            asUnion: true);

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(
            catalog.OpaqueTypes.ContainsKey("some_union"),
            "A typedef that unwraps to a forward-declared union must also be classified as opaque.");
        Assert.IsTrue(
            catalog.OpaqueTypes.ContainsKey("some_union_s"),
            "The underlying union tag must still be registered as opaque by the struct/union classifier.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_AnchorVariablePointsToSubroutineType_RegistersFunction()
    {
        SyntheticCompilationUnit unit = BuildFunctionAnchorScenario(
            anchoredGoName: "some_library_function",
            parameterCount: 1,
            isVariadic: false);

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(
            catalog.Functions.ContainsKey("some_library_function"),
            "An anchor variable whose pointer type resolves to a subroutine_type must register " +
            "as a function in the catalog — this is the only path that surfaces library functions " +
            "such as malloc which never emit DW_TAG_subprogram into the probe's own object file.");

        CgoFunctionInfo function = catalog.Functions["some_library_function"];
        Assert.AreEqual(
            "some_library_function",
            function.Name,
            "The Go-side name embedded in the anchor variable must become the function name.");
        Assert.AreEqual(
            "int",
            function.ReturnType,
            "Subroutine_type DW_AT_type must be formatted as the return C type.");
        Assert.AreEqual(
            1,
            function.Parameters.Count,
            "Every formal_parameter child of the subroutine_type must surface as a parameter.");
        Assert.AreEqual(
            "int",
            function.Parameters[0].CType,
            "Parameter C types must be formatted from the formal_parameter DW_AT_type.");
        Assert.AreEqual(
            "p0",
            function.Parameters[0].Name,
            "Subroutine_type parameters are anonymous in DWARF, so the builder assigns positional p0, p1, ... names.");
        Assert.IsFalse(
            function.IsVariadic,
            "Without a DW_TAG_unspecified_parameters child, the function must not be marked variadic.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_AnchorVariableToSubroutineWithoutReturnType_FunctionReturnsVoid()
    {
        SyntheticCompilationUnit unit = BuildFunctionAnchorScenario(
            anchoredGoName: "do_nothing",
            parameterCount: 0,
            isVariadic: false,
            omitReturnType: true);

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(catalog.Functions.ContainsKey("do_nothing"));
        Assert.AreEqual(
            "void",
            catalog.Functions["do_nothing"].ReturnType,
            "A subroutine_type with no DW_AT_type describes a void-returning function.");
        Assert.AreEqual(
            0,
            catalog.Functions["do_nothing"].Parameters.Count,
            "A subroutine_type with no formal_parameter children produces an empty parameter list.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_AnchorVariableToSubroutineWithVariadicTail_MarksVariadic()
    {
        SyntheticCompilationUnit unit = BuildFunctionAnchorScenario(
            anchoredGoName: "printf_like",
            parameterCount: 1,
            isVariadic: true);

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(catalog.Functions.ContainsKey("printf_like"));
        Assert.IsTrue(
            catalog.Functions["printf_like"].IsVariadic,
            "A DW_TAG_unspecified_parameters child on the subroutine_type must propagate to IsVariadic.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_AnchorVariablePointsToNonFunctionType_DoesNotRegisterFunction()
    {
        SyntheticCompilationUnit unit = BuildIntegerAnchorScenario(
            anchoredGoName: "some_integer_global");

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsFalse(
            catalog.Functions.ContainsKey("some_integer_global"),
            "An anchor variable whose pointer resolves to a base_type (not a subroutine_type) " +
            "must not register as a function — that would mislead the P/Invoke emitter into " +
            "synthesising a stub for a variable or constant.");
    }

    [TestMethod]
    public void PopulateFromCompilationUnit_TopLevelSubprogramWinsOverAnchorVariable_PreservesParameterNames()
    {
        SyntheticCompilationUnit unit = BuildSubprogramAndAnchorScenario(
            functionName: "add",
            firstParameterName: "left",
            secondParameterName: "right");

        CgoSymbolCatalog catalog = new();
        CgoDwarfSymbolSource.PopulateFromCompilationUnit(unit.Build(), catalog);

        Assert.IsTrue(catalog.Functions.ContainsKey("add"));
        CgoFunctionInfo function = catalog.Functions["add"];
        Assert.AreEqual(2, function.Parameters.Count);
        Assert.AreEqual(
            "left",
            function.Parameters[0].Name,
            "When a top-level DW_TAG_subprogram and an anchor variable name the same function, " +
            "the subprogram entry must win because only subprograms carry real parameter names.");
        Assert.AreEqual(
            "right",
            function.Parameters[1].Name,
            "The second parameter must also keep its subprogram-declared name, not a positional fallback.");
    }

    private static SyntheticCompilationUnit BuildFunctionAnchorScenario(
        string anchoredGoName,
        int parameterCount,
        bool isVariadic,
        bool omitReturnType = false)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int subroutineWithReturnAbbrev = unit.DeclareAbbreviation(
            DwarfTag.SubroutineType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int subroutineVoidAbbrev = unit.DeclareAbbreviation(
            DwarfTag.SubroutineType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>());
        int formalParameterAbbrev = unit.DeclareAbbreviation(
            DwarfTag.FormalParameter,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int unspecifiedParametersAbbrev = unit.DeclareAbbreviation(
            DwarfTag.UnspecifiedParameters,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>());
        int pointerTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.PointerType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int variableAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Variable,
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
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("int");
        unit.DebugInfoBuilder.AppendU8(4);

        unit.LabelNextDie("subroutine");
        if (omitReturnType)
        {
            unit.AppendAbbreviationCode(subroutineVoidAbbrev);
        }
        else
        {
            unit.AppendAbbreviationCode(subroutineWithReturnAbbrev);
            unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        }

        for (int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
        {
            unit.AppendAbbreviationCode(formalParameterAbbrev);
            unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        }
        if (isVariadic)
        {
            unit.AppendAbbreviationCode(unspecifiedParametersAbbrev);
        }
        unit.AppendNullDie();

        unit.LabelNextDie("pointer");
        unit.AppendAbbreviationCode(pointerTypeAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("subroutine"));

        unit.AppendAbbreviationCode(variableAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("__ngo_anchor_" + anchoredGoName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("pointer"));

        unit.AppendNullDie();

        return unit;
    }

    private static SyntheticCompilationUnit BuildIntegerAnchorScenario(string anchoredGoName)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int pointerTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.PointerType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int variableAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Variable,
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
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("int");
        unit.DebugInfoBuilder.AppendU8(4);

        unit.LabelNextDie("pointer");
        unit.AppendAbbreviationCode(pointerTypeAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));

        unit.AppendAbbreviationCode(variableAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("__ngo_anchor_" + anchoredGoName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("pointer"));

        unit.AppendNullDie();

        return unit;
    }

    private static SyntheticCompilationUnit BuildSubprogramAndAnchorScenario(
        string functionName,
        string firstParameterName,
        string secondParameterName)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int subprogramAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Subprogram,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int namedFormalParameterAbbrev = unit.DeclareAbbreviation(
            DwarfTag.FormalParameter,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int subroutineAbbrev = unit.DeclareAbbreviation(
            DwarfTag.SubroutineType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int anonymousFormalParameterAbbrev = unit.DeclareAbbreviation(
            DwarfTag.FormalParameter,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int pointerTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.PointerType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int variableAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Variable,
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
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("int");
        unit.DebugInfoBuilder.AppendU8(4);

        unit.AppendAbbreviationCode(subprogramAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(functionName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));

        unit.AppendAbbreviationCode(namedFormalParameterAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(firstParameterName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendAbbreviationCode(namedFormalParameterAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(secondParameterName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();

        unit.LabelNextDie("subroutine");
        unit.AppendAbbreviationCode(subroutineAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendAbbreviationCode(anonymousFormalParameterAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendAbbreviationCode(anonymousFormalParameterAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.AppendNullDie();

        unit.LabelNextDie("pointer");
        unit.AppendAbbreviationCode(pointerTypeAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("subroutine"));

        unit.AppendAbbreviationCode(variableAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("__ngo_anchor_" + functionName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("pointer"));

        unit.AppendNullDie();

        return unit;
    }

    private static SyntheticCompilationUnit BuildOpaqueTypedefScenario(
        string structTagName,
        string typedefName,
        bool asUnion = false)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        DwarfTag opaqueTag = asUnion ? DwarfTag.UnionType : DwarfTag.StructureType;
        int opaqueAbbrev = unit.DeclareAbbreviation(
            opaqueTag,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Declaration, DwarfForm.FlagPresent),
            });
        int typedefAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Typedef,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("opaque");
        unit.AppendAbbreviationCode(opaqueAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(structTagName);
        unit.LabelNextDie("typedef");
        unit.AppendAbbreviationCode(typedefAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(typedefName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("opaque"));
        unit.AppendNullDie();

        return unit;
    }

    private static SyntheticCompilationUnit BuildConcreteTypedefScenario(
        string structTagName,
        string typedefName)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
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
        int memberAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Member,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
                new(DwarfAttribute.DataMemberLocation, DwarfForm.Udata),
            });
        int typedefAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Typedef,
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
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("long");
        unit.DebugInfoBuilder.AppendU8(8);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(structTagName);
        unit.DebugInfoBuilder.AppendU8(8);
        unit.AppendAbbreviationCode(memberAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("seconds");
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.DebugInfoBuilder.AppendUnsignedLeb128(0);
        unit.AppendNullDie();
        unit.LabelNextDie("typedef");
        unit.AppendAbbreviationCode(typedefAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(typedefName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("struct"));
        unit.AppendNullDie();

        return unit;
    }
}
