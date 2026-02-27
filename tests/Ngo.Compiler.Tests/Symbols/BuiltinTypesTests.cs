// -----------------------------------------------------------------------
// <copyright file="BuiltinTypesTests.cs" company="Ziad">
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

using Ngo.Compiler.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Symbols;

[TestClass]
public class BuiltinTypesTests
{
    [DataTestMethod]
    [DataRow("bool", TypeKind.Bool)]
    [DataRow("int", TypeKind.Int)]
    [DataRow("int8", TypeKind.Int8)]
    [DataRow("int16", TypeKind.Int16)]
    [DataRow("int32", TypeKind.Int32)]
    [DataRow("int64", TypeKind.Int64)]
    [DataRow("uint", TypeKind.Uint)]
    [DataRow("uint8", TypeKind.Uint8)]
    [DataRow("uint16", TypeKind.Uint16)]
    [DataRow("uint32", TypeKind.Uint32)]
    [DataRow("uint64", TypeKind.Uint64)]
    [DataRow("float32", TypeKind.Float32)]
    [DataRow("float64", TypeKind.Float64)]
    [DataRow("string", TypeKind.String)]
    public void Resolve_returns_builtin_type(string name, TypeKind expectedKind)
    {
        var type = BuiltinTypes.Resolve(name);
        Assert.IsNotNull(type);
        Assert.AreEqual(expectedKind, type.TypeKind);
        Assert.AreEqual(name, type.Name);
    }

    [TestMethod]
    public void Resolve_byte_returns_uint8_alias()
    {
        var type = BuiltinTypes.Resolve("byte");
        Assert.IsNotNull(type);
        Assert.AreEqual("byte", type.Name);
        Assert.AreEqual(TypeKind.Uint8, type.TypeKind);
        Assert.AreSame(BuiltinTypes.Uint8, type.UnderlyingType);
    }

    [TestMethod]
    public void Resolve_rune_returns_int32_alias()
    {
        var type = BuiltinTypes.Resolve("rune");
        Assert.IsNotNull(type);
        Assert.AreEqual("rune", type.Name);
        Assert.AreEqual(TypeKind.Int32, type.TypeKind);
        Assert.AreSame(BuiltinTypes.Int32, type.UnderlyingType);
    }

    [TestMethod]
    public void Resolve_unknown_returns_null()
    {
        Assert.IsNull(BuiltinTypes.Resolve("foo"));
        Assert.IsNull(BuiltinTypes.Resolve(""));
    }

    [TestMethod]
    public void Error_type_has_error_kind()
    {
        Assert.AreEqual(TypeKind.Error, TypeSymbol.Error.TypeKind);
        Assert.AreEqual("$$error", TypeSymbol.Error.Name);
    }

    [TestMethod]
    public void Void_type_has_void_kind()
    {
        Assert.AreEqual(TypeKind.Void, BuiltinTypes.Void.TypeKind);
    }

    [TestMethod]
    public void Untyped_constants_have_correct_kinds()
    {
        Assert.AreEqual(TypeKind.UntypedBool, BuiltinTypes.UntypedBool.TypeKind);
        Assert.AreEqual(TypeKind.UntypedInt, BuiltinTypes.UntypedInt.TypeKind);
        Assert.AreEqual(TypeKind.UntypedFloat, BuiltinTypes.UntypedFloat.TypeKind);
        Assert.AreEqual(TypeKind.UntypedString, BuiltinTypes.UntypedString.TypeKind);
        Assert.AreEqual(TypeKind.UntypedNil, BuiltinTypes.UntypedNil.TypeKind);
    }

    [TestMethod]
    public void All_symbol_kinds_are_type()
    {
        Assert.AreEqual(SymbolKind.Type, BuiltinTypes.Bool.Kind);
        Assert.AreEqual(SymbolKind.Type, BuiltinTypes.Int.Kind);
        Assert.AreEqual(SymbolKind.Type, BuiltinTypes.String.Kind);
        Assert.AreEqual(SymbolKind.Type, BuiltinTypes.Byte.Kind);
        Assert.AreEqual(SymbolKind.Type, BuiltinTypes.Rune.Kind);
    }
}
