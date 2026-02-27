// -----------------------------------------------------------------------
// <copyright file="TypeCheckerTests.cs" company="Ziad">
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

using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Semantics;

[TestClass]
public class TypeCheckerTests
{
    [DataTestMethod]
    [DataRow(TypeKind.Int)]
    [DataRow(TypeKind.Int8)]
    [DataRow(TypeKind.Int16)]
    [DataRow(TypeKind.Int32)]
    [DataRow(TypeKind.Int64)]
    [DataRow(TypeKind.Uint)]
    [DataRow(TypeKind.Uint8)]
    [DataRow(TypeKind.Uint16)]
    [DataRow(TypeKind.Uint32)]
    [DataRow(TypeKind.Uint64)]
    public void IsInteger_returns_true_for_integer_types(TypeKind kind)
    {
        var type = new TypeSymbol("test", kind, null);
        Assert.IsTrue(TypeChecker.IsInteger(type));
        Assert.IsTrue(TypeChecker.IsNumeric(type));
    }

    [DataTestMethod]
    [DataRow(TypeKind.Float32)]
    [DataRow(TypeKind.Float64)]
    public void IsFloat_returns_true_for_float_types(TypeKind kind)
    {
        var type = new TypeSymbol("test", kind, null);
        Assert.IsTrue(TypeChecker.IsFloat(type));
        Assert.IsTrue(TypeChecker.IsNumeric(type));
    }

    [TestMethod]
    public void IsNumeric_returns_false_for_non_numeric()
    {
        Assert.IsFalse(TypeChecker.IsNumeric(BuiltinTypes.Bool));
        Assert.IsFalse(TypeChecker.IsNumeric(BuiltinTypes.String));
    }

    [TestMethod]
    public void IsUntyped_identifies_untyped_constants()
    {
        Assert.IsTrue(TypeChecker.IsUntyped(BuiltinTypes.UntypedInt));
        Assert.IsTrue(TypeChecker.IsUntyped(BuiltinTypes.UntypedFloat));
        Assert.IsTrue(TypeChecker.IsUntyped(BuiltinTypes.UntypedBool));
        Assert.IsTrue(TypeChecker.IsUntyped(BuiltinTypes.UntypedString));
        Assert.IsTrue(TypeChecker.IsUntyped(BuiltinTypes.UntypedNil));
        Assert.IsFalse(TypeChecker.IsUntyped(BuiltinTypes.Int));
        Assert.IsFalse(TypeChecker.IsUntyped(BuiltinTypes.String));
    }

    [DataTestMethod]
    [DataRow(TypeKind.UntypedBool, TypeKind.Bool)]
    [DataRow(TypeKind.UntypedInt, TypeKind.Int)]
    [DataRow(TypeKind.UntypedFloat, TypeKind.Float64)]
    [DataRow(TypeKind.UntypedString, TypeKind.String)]
    public void DefaultType_returns_concrete_type(TypeKind untypedKind, TypeKind expectedKind)
    {
        var untyped = new TypeSymbol("test", untypedKind, null);
        var result = TypeChecker.DefaultType(untyped);
        Assert.AreEqual(expectedKind, result.TypeKind);
    }

    [TestMethod]
    public void DefaultType_returns_same_for_concrete()
    {
        Assert.AreSame(BuiltinTypes.Int, TypeChecker.DefaultType(BuiltinTypes.Int));
        Assert.AreSame(BuiltinTypes.String, TypeChecker.DefaultType(BuiltinTypes.String));
    }

    [TestMethod]
    public void IsAssignable_same_type()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.Int, BuiltinTypes.Int));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.String, BuiltinTypes.String));
    }

    [TestMethod]
    public void IsAssignable_error_type_is_always_assignable()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(TypeSymbol.Error, BuiltinTypes.Int));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.Int, TypeSymbol.Error));
    }

    [TestMethod]
    public void IsAssignable_untyped_int_to_integers()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedInt, BuiltinTypes.Int));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedInt, BuiltinTypes.Int64));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedInt, BuiltinTypes.Uint8));
    }

    [TestMethod]
    public void IsAssignable_untyped_int_to_floats()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedInt, BuiltinTypes.Float32));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedInt, BuiltinTypes.Float64));
    }

    [TestMethod]
    public void IsAssignable_untyped_float_to_floats()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedFloat, BuiltinTypes.Float32));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.UntypedFloat, BuiltinTypes.Float64));
    }

    [TestMethod]
    public void IsAssignable_rejects_incompatible()
    {
        Assert.IsFalse(TypeChecker.IsAssignable(BuiltinTypes.Int, BuiltinTypes.String));
        Assert.IsFalse(TypeChecker.IsAssignable(BuiltinTypes.Bool, BuiltinTypes.Int));
        Assert.IsFalse(TypeChecker.IsAssignable(BuiltinTypes.UntypedFloat, BuiltinTypes.Int));
    }

    [TestMethod]
    public void IsAssignable_byte_uint8_alias()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.Byte, BuiltinTypes.Uint8));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.Uint8, BuiltinTypes.Byte));
    }

    [TestMethod]
    public void IsAssignable_rune_int32_alias()
    {
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.Rune, BuiltinTypes.Int32));
        Assert.IsTrue(TypeChecker.IsAssignable(BuiltinTypes.Int32, BuiltinTypes.Rune));
    }

    [TestMethod]
    public void CommonType_same_type()
    {
        Assert.AreSame(BuiltinTypes.Int, TypeChecker.CommonType(BuiltinTypes.Int, BuiltinTypes.Int));
    }

    [TestMethod]
    public void CommonType_error_propagates()
    {
        Assert.AreSame(TypeSymbol.Error, TypeChecker.CommonType(TypeSymbol.Error, BuiltinTypes.Int));
        Assert.AreSame(TypeSymbol.Error, TypeChecker.CommonType(BuiltinTypes.Int, TypeSymbol.Error));
    }

    [TestMethod]
    public void CommonType_untyped_with_typed()
    {
        Assert.AreSame(BuiltinTypes.Int, TypeChecker.CommonType(BuiltinTypes.UntypedInt, BuiltinTypes.Int));
        Assert.AreSame(BuiltinTypes.Int, TypeChecker.CommonType(BuiltinTypes.Int, BuiltinTypes.UntypedInt));
    }

    [TestMethod]
    public void CommonType_untyped_int_with_untyped_float()
    {
        Assert.AreSame(BuiltinTypes.UntypedFloat, TypeChecker.CommonType(BuiltinTypes.UntypedInt, BuiltinTypes.UntypedFloat));
        Assert.AreSame(BuiltinTypes.UntypedFloat, TypeChecker.CommonType(BuiltinTypes.UntypedFloat, BuiltinTypes.UntypedInt));
    }

    [TestMethod]
    public void CommonType_incompatible_returns_null()
    {
        Assert.IsNull(TypeChecker.CommonType(BuiltinTypes.Int, BuiltinTypes.String));
        Assert.IsNull(TypeChecker.CommonType(BuiltinTypes.Bool, BuiltinTypes.Float64));
    }

    [TestMethod]
    public void CanConvert_numeric_to_numeric()
    {
        Assert.IsTrue(TypeChecker.CanConvert(BuiltinTypes.Int, BuiltinTypes.Float64));
        Assert.IsTrue(TypeChecker.CanConvert(BuiltinTypes.Float64, BuiltinTypes.Int));
        Assert.IsTrue(TypeChecker.CanConvert(BuiltinTypes.Int8, BuiltinTypes.Int64));
    }

    [TestMethod]
    public void CanConvert_string_integer()
    {
        Assert.IsTrue(TypeChecker.CanConvert(BuiltinTypes.String, BuiltinTypes.Int));
        Assert.IsTrue(TypeChecker.CanConvert(BuiltinTypes.Int, BuiltinTypes.String));
    }

    [TestMethod]
    public void CanConvert_rejects_bool_to_int()
    {
        Assert.IsFalse(TypeChecker.CanConvert(BuiltinTypes.Bool, BuiltinTypes.Int));
    }

    [TestMethod]
    public void UntypedInt_is_integer()
    {
        Assert.IsTrue(TypeChecker.IsInteger(BuiltinTypes.UntypedInt));
    }

    [TestMethod]
    public void UntypedFloat_is_float()
    {
        Assert.IsTrue(TypeChecker.IsFloat(BuiltinTypes.UntypedFloat));
    }
}
