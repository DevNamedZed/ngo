// -----------------------------------------------------------------------
// <copyright file="CgoSymbolCatalogSerializerTests.cs" company="Ziad">
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;

namespace Ngo.Compiler.Tests.Cgo;

/// <summary>
/// Round-trip and validation tests for the catalog.json persistence
/// layer. The serializer is the only interchange format between the
/// DWARF/PDB readers and cache hits, so silent field drops or type
/// coercions must be caught here.
/// </summary>
[TestClass]
public class CgoSymbolCatalogSerializerTests
{
    [TestMethod]
    public void EmptyCatalogRoundTripsWithNoEntries()
    {
        var original = new CgoSymbolCatalog();

        string json = CgoSymbolCatalogSerializer.Serialize(original);
        CgoSymbolCatalog restored = CgoSymbolCatalogSerializer.Deserialize(json);

        Assert.AreEqual(0, restored.Typedefs.Count);
        Assert.AreEqual(0, restored.StructsAndUnions.Count);
        Assert.AreEqual(0, restored.Enums.Count);
        Assert.AreEqual(0, restored.Functions.Count);
        Assert.AreEqual(0, restored.FunctionPointers.Count);
        Assert.AreEqual(0, restored.OpaqueTypes.Count);
        Assert.AreEqual(0, restored.MacroConstants.Count);
    }

    [TestMethod]
    public void PopulatedCatalogPreservesEveryBucketRoundTrip()
    {
        var original = new CgoSymbolCatalog();
        original.AddTypedef(new CgoTypedefInfo("size_t", "unsigned long"));
        original.AddTypedef(new CgoTypedefInfo("ZSTD_CCtx_ptr", "struct ZSTD_CCtx *"));

        var pointStruct = new CgoStructInfo(
            cName: "struct point",
            goName: "point",
            fields: new List<CgoFieldInfo>
            {
                new CgoFieldInfo(
                    name: "x", cType: "int",
                    offsetBytes: 0, sizeBytes: 4, bitOffset: 0, bitSize: 0),
                new CgoFieldInfo(
                    name: "y", cType: "int",
                    offsetBytes: 4, sizeBytes: 4, bitOffset: 0, bitSize: 0),
            },
            isUnion: false,
            sizeBytes: 8,
            alignmentBytes: 4);
        original.AddStructOrUnion(pointStruct);

        var variantUnion = new CgoStructInfo(
            cName: "union variant",
            goName: "variant",
            fields: new List<CgoFieldInfo>
            {
                new CgoFieldInfo(
                    name: "asInt", cType: "int",
                    offsetBytes: 0, sizeBytes: 4, bitOffset: 0, bitSize: 0),
                new CgoFieldInfo(
                    name: "asFloat", cType: "float",
                    offsetBytes: 0, sizeBytes: 4, bitOffset: 0, bitSize: 0),
            },
            isUnion: true,
            sizeBytes: 4,
            alignmentBytes: 4);
        original.AddStructOrUnion(variantUnion);

        original.AddEnum(new CgoEnumInfo(
            "Color",
            "int",
            new List<CgoEnumValue>
            {
                new CgoEnumValue("RED", 0),
                new CgoEnumValue("GREEN", 1),
                new CgoEnumValue("BLUE", 2),
            }));

        var addFunction = new CgoFunctionInfo
        {
            Name = "add",
            ReturnType = "int",
            IsVariadic = false,
        };
        addFunction.Parameters.Add(new CgoParameterInfo { Name = "a", CType = "int" });
        addFunction.Parameters.Add(new CgoParameterInfo { Name = "b", CType = "int" });
        original.AddFunction(addFunction);

        var printfFunction = new CgoFunctionInfo
        {
            Name = "printf",
            ReturnType = "int",
            IsVariadic = true,
        };
        printfFunction.Parameters.Add(new CgoParameterInfo { Name = "format", CType = "const char *" });
        original.AddFunction(printfFunction);

        original.AddFunctionPointer(new CgoFunctionPointerInfo(
            "sqlite3_callback",
            "int",
            new List<string> { "void *", "int", "char **", "char **" },
            isVariadic: false));

        original.AddOpaqueType(new CgoOpaqueTypeInfo("sqlite3"));
        original.AddOpaqueType(new CgoOpaqueTypeInfo("ZSTD_CCtx"));

        original.AddMacroConstant(new CgoMacroConstantInfo("CKA_CLASS", 0, "unsigned long"));
        original.AddMacroConstant(new CgoMacroConstantInfo("SQLITE_OK", 0, "int"));
        original.AddMacroConstant(new CgoMacroConstantInfo("INT64_NEG", -9223372036854775807L, "long long"));

        string json = CgoSymbolCatalogSerializer.Serialize(original);
        CgoSymbolCatalog restored = CgoSymbolCatalogSerializer.Deserialize(json);

        Assert.AreEqual(2, restored.Typedefs.Count);
        Assert.AreEqual("unsigned long", restored.Typedefs["size_t"].AliasCType);
        Assert.AreEqual("struct ZSTD_CCtx *", restored.Typedefs["ZSTD_CCtx_ptr"].AliasCType);

        Assert.AreEqual(2, restored.StructsAndUnions.Count);
        CgoStructInfo restoredPoint = restored.StructsAndUnions["point"];
        Assert.AreEqual("struct point", restoredPoint.CName);
        Assert.IsFalse(restoredPoint.IsUnion);
        Assert.AreEqual(8L, restoredPoint.SizeBytes);
        Assert.AreEqual(4L, restoredPoint.AlignmentBytes);
        Assert.AreEqual(2, restoredPoint.Fields.Count);
        Assert.AreEqual("x", restoredPoint.Fields[0].Name);
        Assert.AreEqual("int", restoredPoint.Fields[0].CType);
        Assert.AreEqual(0L, restoredPoint.Fields[0].OffsetBytes);
        Assert.AreEqual(4L, restoredPoint.Fields[0].SizeBytes);
        Assert.AreEqual(0, restoredPoint.Fields[0].BitOffset);
        Assert.AreEqual(0, restoredPoint.Fields[0].BitSize);
        Assert.IsFalse(restoredPoint.Fields[0].IsBitfield);
        Assert.AreEqual("y", restoredPoint.Fields[1].Name);
        Assert.AreEqual(4L, restoredPoint.Fields[1].OffsetBytes);

        CgoStructInfo restoredVariant = restored.StructsAndUnions["variant"];
        Assert.IsTrue(restoredVariant.IsUnion);
        Assert.AreEqual(4L, restoredVariant.SizeBytes);
        Assert.AreEqual("float", restoredVariant.Fields[1].CType);
        Assert.AreEqual(0L, restoredVariant.Fields[1].OffsetBytes);

        Assert.AreEqual(1, restored.Enums.Count);
        CgoEnumInfo restoredColor = restored.Enums["Color"];
        Assert.AreEqual("int", restoredColor.UnderlyingCType);
        Assert.AreEqual(3, restoredColor.Values.Count);
        Assert.AreEqual("GREEN", restoredColor.Values[1].Name);
        Assert.AreEqual(1L, restoredColor.Values[1].Value);

        Assert.AreEqual(2, restored.Functions.Count);
        CgoFunctionInfo restoredAdd = restored.Functions["add"];
        Assert.AreEqual("int", restoredAdd.ReturnType);
        Assert.IsFalse(restoredAdd.IsVariadic);
        Assert.AreEqual(2, restoredAdd.Parameters.Count);
        Assert.AreEqual("b", restoredAdd.Parameters[1].Name);

        CgoFunctionInfo restoredPrintf = restored.Functions["printf"];
        Assert.IsTrue(restoredPrintf.IsVariadic);

        Assert.AreEqual(1, restored.FunctionPointers.Count);
        CgoFunctionPointerInfo restoredCallback = restored.FunctionPointers["sqlite3_callback"];
        Assert.AreEqual("int", restoredCallback.ReturnCType);
        Assert.AreEqual(4, restoredCallback.ParameterCTypes.Count);
        Assert.AreEqual("char **", restoredCallback.ParameterCTypes[2]);

        Assert.AreEqual(2, restored.OpaqueTypes.Count);
        Assert.IsTrue(restored.OpaqueTypes.ContainsKey("sqlite3"));
        Assert.IsTrue(restored.OpaqueTypes.ContainsKey("ZSTD_CCtx"));

        Assert.AreEqual(3, restored.MacroConstants.Count);
        Assert.AreEqual(0L, restored.MacroConstants["CKA_CLASS"].Value);
        Assert.AreEqual("unsigned long", restored.MacroConstants["CKA_CLASS"].UnderlyingCType);
        Assert.AreEqual(-9223372036854775807L, restored.MacroConstants["INT64_NEG"].Value);
    }

    [TestMethod]
    public void SerializedOutputIsDeterministicAcrossInsertionOrders()
    {
        var first = new CgoSymbolCatalog();
        first.AddTypedef(new CgoTypedefInfo("alpha", "int"));
        first.AddTypedef(new CgoTypedefInfo("beta", "char"));

        var second = new CgoSymbolCatalog();
        second.AddTypedef(new CgoTypedefInfo("beta", "char"));
        second.AddTypedef(new CgoTypedefInfo("alpha", "int"));

        Assert.AreEqual(
            CgoSymbolCatalogSerializer.Serialize(first),
            CgoSymbolCatalogSerializer.Serialize(second));
    }

    [TestMethod]
    public void DeserializeRejectsMissingVersionField()
    {
        string json = "{\"typedefs\":{},\"structsAndUnions\":{},\"enums\":{}," +
                      "\"functions\":{},\"functionPointers\":{}," +
                      "\"opaqueTypes\":[],\"macroConstants\":{}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "version");
    }

    [TestMethod]
    public void DeserializeRejectsUnsupportedFormatVersion()
    {
        string json = "{\"version\":99,\"typedefs\":{},\"structsAndUnions\":{},\"enums\":{}," +
                      "\"functions\":{},\"functionPointers\":{}," +
                      "\"opaqueTypes\":[],\"macroConstants\":{}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "version");
    }

    [TestMethod]
    public void DeserializeRejectsPreviousFormatVersionOne()
    {
        string json = "{\"version\":1,\"typedefs\":{},\"structsAndUnions\":{},\"enums\":{}," +
                      "\"functions\":{},\"functionPointers\":{}," +
                      "\"opaqueTypes\":[],\"macroConstants\":{}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "version");
    }

    [TestMethod]
    public void DeserializeRejectsTypedefWithoutAliasCType()
    {
        string json = "{\"version\":2,\"typedefs\":{\"broken\":{}}," +
                      "\"structsAndUnions\":{},\"enums\":{},\"functions\":{}," +
                      "\"functionPointers\":{},\"opaqueTypes\":[],\"macroConstants\":{}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "aliasCType");
    }

    [TestMethod]
    public void DeserializeRejectsMacroConstantWithStringValue()
    {
        string json = "{\"version\":2,\"typedefs\":{},\"structsAndUnions\":{},\"enums\":{}," +
                      "\"functions\":{},\"functionPointers\":{},\"opaqueTypes\":[]," +
                      "\"macroConstants\":{\"BAD\":{\"value\":\"zero\",\"underlyingCType\":\"int\"}}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "value");
    }

    [TestMethod]
    public void DeserializeRejectsStructMissingSizeBytes()
    {
        string json = "{\"version\":2,\"typedefs\":{}," +
                      "\"structsAndUnions\":{\"broken\":{\"cName\":\"struct broken\"," +
                      "\"isUnion\":false,\"alignmentBytes\":4,\"fields\":[]}}," +
                      "\"enums\":{},\"functions\":{},\"functionPointers\":{}," +
                      "\"opaqueTypes\":[],\"macroConstants\":{}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "sizeBytes");
    }

    [TestMethod]
    public void DeserializeRejectsFieldMissingBitfieldAttributes()
    {
        string json = "{\"version\":2,\"typedefs\":{}," +
                      "\"structsAndUnions\":{\"broken\":{\"cName\":\"struct broken\"," +
                      "\"isUnion\":false,\"sizeBytes\":4,\"alignmentBytes\":4," +
                      "\"fields\":[{\"name\":\"f\",\"cType\":\"int\"," +
                      "\"offsetBytes\":0,\"sizeBytes\":4,\"bitOffset\":0}]}}," +
                      "\"enums\":{},\"functions\":{},\"functionPointers\":{}," +
                      "\"opaqueTypes\":[],\"macroConstants\":{}}";

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(
            () => CgoSymbolCatalogSerializer.Deserialize(json));
        StringAssert.Contains(exception.Message, "bitSize");
    }

    [TestMethod]
    public void RoundTripPreservesBitfieldLayout()
    {
        var original = new CgoSymbolCatalog();
        original.AddStructOrUnion(new CgoStructInfo(
            cName: "struct flags",
            goName: "flags",
            fields: new List<CgoFieldInfo>
            {
                new CgoFieldInfo(
                    name: "enabled", cType: "unsigned int",
                    offsetBytes: 0, sizeBytes: 4, bitOffset: 0, bitSize: 1),
                new CgoFieldInfo(
                    name: "mode", cType: "unsigned int",
                    offsetBytes: 0, sizeBytes: 4, bitOffset: 1, bitSize: 3),
            },
            isUnion: false,
            sizeBytes: 4,
            alignmentBytes: 4));

        string json = CgoSymbolCatalogSerializer.Serialize(original);
        CgoSymbolCatalog restored = CgoSymbolCatalogSerializer.Deserialize(json);

        CgoStructInfo restoredFlags = restored.StructsAndUnions["flags"];
        Assert.AreEqual(2, restoredFlags.Fields.Count);
        Assert.IsTrue(restoredFlags.Fields[0].IsBitfield);
        Assert.AreEqual(0, restoredFlags.Fields[0].BitOffset);
        Assert.AreEqual(1, restoredFlags.Fields[0].BitSize);
        Assert.IsTrue(restoredFlags.Fields[1].IsBitfield);
        Assert.AreEqual(1, restoredFlags.Fields[1].BitOffset);
        Assert.AreEqual(3, restoredFlags.Fields[1].BitSize);
    }
}
