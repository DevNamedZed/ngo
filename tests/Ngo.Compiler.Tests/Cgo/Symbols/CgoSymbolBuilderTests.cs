// -----------------------------------------------------------------------
// <copyright file="CgoSymbolBuilderTests.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Tests.Cgo.Symbols;

/// <summary>
/// Unit tests for <see cref="CgoSymbolBuilder"/>. The builder is the
/// seam between the reader-agnostic <see cref="CgoSymbolCatalog"/> and
/// the semantic <see cref="PackageSymbol"/> that Go code binds against
/// when resolving <c>C.foo</c>. These tests drive the builder from
/// synthetic catalogs so every contract — function shape, struct
/// shape, enum and macro constant exposure, sizeof projection — is
/// pinned independently from the DWARF or PDB reader that normally
/// produces the catalog.
/// </summary>
[TestClass]
public class CgoSymbolBuilderTests
{
    [TestMethod]
    public void BuildCPackage_AlwaysExposesPrimitiveTypeAliases()
    {
        PackageSymbol package = BuildPackage(new CgoSymbolCatalog(), new CgoProbeResult());

        foreach (string primitive in new[]
        {
            "char", "schar", "uchar", "short", "ushort", "int", "uint",
            "long", "ulong", "longlong", "ulonglong", "float", "double", "size_t",
        })
        {
            Assert.IsInstanceOfType(
                package.LookupExport(primitive),
                typeof(TypeSymbol),
                $"Primitive alias '{primitive}' must always be exported on the C pseudo-package.");
        }
    }

    [TestMethod]
    public void BuildCPackage_AlwaysExposesMarshallingHelpers()
    {
        PackageSymbol package = BuildPackage(new CgoSymbolCatalog(), new CgoProbeResult());

        foreach (string helper in new[] { "CString", "GoString", "GoStringN", "GoBytes", "CBytes", "free" })
        {
            Assert.IsInstanceOfType(
                package.LookupExport(helper),
                typeof(FunctionSymbol),
                $"Marshalling helper '{helper}' must always be exported on the C pseudo-package.");
        }
    }

    [TestMethod]
    public void BuildCPackage_CatalogFunctionBecomesExportedFunctionSymbol()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddFunction(new CgoFunctionInfo
        {
            Name = "sqlite3_open",
            ReturnType = "int",
            Parameters = new List<CgoParameterInfo>
            {
                new() { Name = "filename", CType = "const char *" },
                new() { Name = "ppDb", CType = "sqlite3 **" },
            },
            IsVariadic = false,
        });

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        FunctionSymbol? function = package.LookupExport("sqlite3_open") as FunctionSymbol;
        Assert.IsNotNull(function, "Catalog functions must be exported as FunctionSymbols.");
        Assert.AreEqual(2, function!.Parameters.Count, "Parameter count must be preserved.");
        Assert.AreEqual("filename", function.Parameters[0].Name);
        Assert.AreEqual("ppDb", function.Parameters[1].Name);
        Assert.AreEqual(1, function.ReturnTypes.Count, "Non-void return types must be exposed.");
        Assert.IsFalse(function.IsVariadic);
    }

    [TestMethod]
    public void BuildCPackage_VoidReturnFunctionHasNoReturnTypes()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddFunction(new CgoFunctionInfo
        {
            Name = "sqlite3_free",
            ReturnType = "void",
            Parameters = new List<CgoParameterInfo>
            {
                new() { Name = "p", CType = "void *" },
            },
        });

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        FunctionSymbol? function = package.LookupExport("sqlite3_free") as FunctionSymbol;
        Assert.IsNotNull(function);
        Assert.AreEqual(0, function!.ReturnTypes.Count,
            "Void-returning C functions must expose an empty ReturnTypes list so Go-side analysis treats them as statements.");
    }

    [TestMethod]
    public void BuildCPackage_VariadicFunctionKeepsVariadicFlag()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddFunction(new CgoFunctionInfo
        {
            Name = "printf",
            ReturnType = "int",
            Parameters = new List<CgoParameterInfo>
            {
                new() { Name = "format", CType = "const char *" },
            },
            IsVariadic = true,
        });

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        FunctionSymbol? function = package.LookupExport("printf") as FunctionSymbol;
        Assert.IsNotNull(function);
        Assert.IsTrue(function!.IsVariadic, "Variadic flag from catalog must propagate to FunctionSymbol.");
    }

    [TestMethod]
    public void BuildCPackage_CatalogStructBecomesStructTypeSymbol()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddStructOrUnion(new CgoStructInfo(
            cName: "struct point",
            goName: "point",
            fields: new List<CgoFieldInfo>
            {
                new("x", "int", offsetBytes: 0, sizeBytes: 4, bitOffset: 0, bitSize: 0),
                new("y", "int", offsetBytes: 4, sizeBytes: 4, bitOffset: 0, bitSize: 0),
            },
            isUnion: false,
            sizeBytes: 8,
            alignmentBytes: 4));

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        StructTypeSymbol? structSym = package.LookupExport("point") as StructTypeSymbol;
        Assert.IsNotNull(structSym, "Catalog structs must be exported as StructTypeSymbols keyed by their Go-visible name.");
        Assert.AreEqual(2, structSym!.Fields.Count);
        Assert.AreEqual("x", structSym.Fields[0].Name);
        Assert.AreEqual("y", structSym.Fields[1].Name);
    }

    [TestMethod]
    public void BuildCPackage_EnumValuesBecomeConstantSymbols()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddEnum(new CgoEnumInfo(
            name: "Color",
            underlyingCType: "int",
            values: new List<CgoEnumValue>
            {
                new("RED", 0),
                new("GREEN", 1),
                new("BLUE", 2),
            }));

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        ConstantSymbol? red = package.LookupExport("RED") as ConstantSymbol;
        ConstantSymbol? green = package.LookupExport("GREEN") as ConstantSymbol;
        ConstantSymbol? blue = package.LookupExport("BLUE") as ConstantSymbol;

        Assert.IsNotNull(red);
        Assert.IsNotNull(green);
        Assert.IsNotNull(blue);
        Assert.AreEqual(0L, red!.Value);
        Assert.AreEqual(1L, green!.Value);
        Assert.AreEqual(2L, blue!.Value);
    }

    [TestMethod]
    public void BuildCPackage_MacroConstantsBecomeConstantSymbols()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddMacroConstant(new CgoMacroConstantInfo("CKA_CLASS", 0L, "unsigned long"));
        catalog.AddMacroConstant(new CgoMacroConstantInfo("CKA_TOKEN", 1L, "unsigned long"));

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        ConstantSymbol? ckaClass = package.LookupExport("CKA_CLASS") as ConstantSymbol;
        ConstantSymbol? ckaToken = package.LookupExport("CKA_TOKEN") as ConstantSymbol;

        Assert.IsNotNull(ckaClass);
        Assert.IsNotNull(ckaToken);
        Assert.AreEqual(0L, ckaClass!.Value);
        Assert.AreEqual(1L, ckaToken!.Value);
    }

    [TestMethod]
    public void BuildCPackage_SizeofConstantsComeFromProbeWhenAvailable()
    {
        CgoProbeResult probeResult = new();
        probeResult.TypeSizes["int"] = 4;
        probeResult.TypeSizes["long"] = 8;
        probeResult.TypeSizes["void_ptr"] = 8;

        PackageSymbol package = BuildPackage(new CgoSymbolCatalog(), probeResult);

        ConstantSymbol? sizeofInt = package.LookupExport("sizeof_int") as ConstantSymbol;
        ConstantSymbol? sizeofLong = package.LookupExport("sizeof_long") as ConstantSymbol;
        ConstantSymbol? sizeofVoidPtr = package.LookupExport("sizeof_void_ptr") as ConstantSymbol;

        Assert.IsNotNull(sizeofInt);
        Assert.IsNotNull(sizeofLong);
        Assert.IsNotNull(sizeofVoidPtr);
        Assert.AreEqual(4L, sizeofInt!.Value);
        Assert.AreEqual(8L, sizeofLong!.Value);
        Assert.AreEqual(8L, sizeofVoidPtr!.Value);
    }

    [TestMethod]
    public void BuildCPackage_StructSizeofConstantIsExportedFromCatalog()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddStructOrUnion(new CgoStructInfo(
            cName: "struct sockaddr_in",
            goName: "sockaddr_in",
            fields: new List<CgoFieldInfo>(),
            isUnion: false,
            sizeBytes: 16,
            alignmentBytes: 4));

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        ConstantSymbol? sizeofStruct = package.LookupExport("sizeof_sockaddr_in") as ConstantSymbol;
        Assert.IsNotNull(sizeofStruct,
            "Each catalog struct must contribute a sizeof_<GoName> constant so C.sizeof_* resolves.");
        Assert.AreEqual(16L, sizeofStruct!.Value);
    }

    [TestMethod]
    public void BuildCPackage_ProbeSizeofConstantsDoNotOverrideExistingEntries()
    {
        CgoProbeResult probeResult = new();
        probeResult.TypeSizes["int"] = 4;
        probeResult.TypeSizes["custom_type"] = 12;

        PackageSymbol package = BuildPackage(new CgoSymbolCatalog(), probeResult);

        ConstantSymbol? sizeofInt = package.LookupExport("sizeof_int") as ConstantSymbol;
        ConstantSymbol? sizeofCustom = package.LookupExport("sizeof_custom_type") as ConstantSymbol;

        Assert.IsNotNull(sizeofInt);
        Assert.AreEqual(4L, sizeofInt!.Value,
            "Standard sizeof_int must be written first and not overwritten by the secondary probe loop.");
        Assert.IsNotNull(sizeofCustom);
        Assert.AreEqual(12L, sizeofCustom!.Value);
    }

    [TestMethod]
    public void BuildCPackage_CatalogNameCollisionDoesNotReplaceEarlierEntry()
    {
        CgoSymbolCatalog catalog = new();
        catalog.AddMacroConstant(new CgoMacroConstantInfo("FOO", 7L, "int"));
        catalog.AddEnum(new CgoEnumInfo(
            name: "Anonymous",
            underlyingCType: "int",
            values: new List<CgoEnumValue> { new("FOO", 999L) }));

        PackageSymbol package = BuildPackage(catalog, new CgoProbeResult());

        ConstantSymbol? foo = package.LookupExport("FOO") as ConstantSymbol;
        Assert.IsNotNull(foo);
        Assert.AreEqual(999L, foo!.Value,
            "Catalog population order is enum-before-macro; the first-win guard keeps whichever lands first.");
    }

    private static PackageSymbol BuildPackage(CgoSymbolCatalog catalog, CgoProbeResult probeResult)
    {
        CgoSymbolBuilder builder = new(catalog, probeResult);
        return builder.BuildCPackage("ngo_native");
    }
}
