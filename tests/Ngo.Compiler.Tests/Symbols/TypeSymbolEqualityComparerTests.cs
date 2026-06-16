// -----------------------------------------------------------------------
// <copyright file="TypeSymbolEqualityComparerTests.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Symbols;

[TestClass]
public class TypeSymbolEqualityComparerTests
{
    private static readonly TypeSymbolEqualityComparer Comparer = TypeSymbolEqualityComparer.Instance;

    private static StructTypeSymbol NamedType(string name, string? package)
    {
        return new StructTypeSymbol(name, Array.Empty<FieldSymbol>(), package);
    }

    [TestMethod]
    public void Equals_SameInstance_ReturnsTrue()
    {
        var type = NamedType("Foo", "pkg");
        Assert.IsTrue(Comparer.Equals(type, type));
    }

    [TestMethod]
    public void Equals_NamedTypesWithSamePackageAndName_ReturnsTrue()
    {
        Assert.IsTrue(Comparer.Equals(NamedType("Foo", "pkg"), NamedType("Foo", "pkg")));
    }

    [TestMethod]
    public void Equals_NamedTypesWithSameNameDifferentPackage_ReturnsFalse()
    {
        Assert.IsFalse(Comparer.Equals(NamedType("Foo", "pkg/one"), NamedType("Foo", "pkg/two")));
    }

    [TestMethod]
    public void Equals_NamedTypesWithDifferentName_ReturnsFalse()
    {
        Assert.IsFalse(Comparer.Equals(NamedType("Foo", "pkg"), NamedType("Bar", "pkg")));
    }

    [TestMethod]
    public void Equals_PointersToEquivalentElement_ReturnsTrue()
    {
        var pointerOne = new PointerTypeSymbol(NamedType("Foo", "pkg"));
        var pointerTwo = new PointerTypeSymbol(NamedType("Foo", "pkg"));
        Assert.IsTrue(Comparer.Equals(pointerOne, pointerTwo));
    }

    [TestMethod]
    public void Equals_PointersToDifferentElement_ReturnsFalse()
    {
        var pointerToFoo = new PointerTypeSymbol(NamedType("Foo", "pkg"));
        var pointerToBar = new PointerTypeSymbol(NamedType("Bar", "pkg"));
        Assert.IsFalse(Comparer.Equals(pointerToFoo, pointerToBar));
    }

    // The regression case: both slices have the composed name "[]Foo" because the element
    // name is embedded without its package, but the elements are from different packages.
    // Name-only comparison (the old behaviour) wrongly treated these as equal.
    [TestMethod]
    public void Equals_SlicesOfSameNamedTypeFromDifferentPackages_ReturnsFalse()
    {
        var sliceInPackageOne = new SliceTypeSymbol(NamedType("Foo", "pkg/one"));
        var sliceInPackageTwo = new SliceTypeSymbol(NamedType("Foo", "pkg/two"));

        Assert.AreEqual(sliceInPackageOne.Name, sliceInPackageTwo.Name);
        Assert.IsFalse(Comparer.Equals(sliceInPackageOne, sliceInPackageTwo));
    }

    [TestMethod]
    public void Equals_ArraysWithSameLengthAndElement_ReturnsTrue()
    {
        var arrayOne = new ArrayTypeSymbol(NamedType("Frame", "pkg"), 4);
        var arrayTwo = new ArrayTypeSymbol(NamedType("Frame", "pkg"), 4);
        Assert.IsTrue(Comparer.Equals(arrayOne, arrayTwo));
    }

    [TestMethod]
    public void Equals_ArraysWithDifferentLength_ReturnsFalse()
    {
        var arrayOfFour = new ArrayTypeSymbol(NamedType("Frame", "pkg"), 4);
        var arrayOfEight = new ArrayTypeSymbol(NamedType("Frame", "pkg"), 8);
        Assert.IsFalse(Comparer.Equals(arrayOfFour, arrayOfEight));
    }

    [TestMethod]
    public void Equals_MapsWithEquivalentKeyAndValue_ReturnsTrue()
    {
        var mapOne = new MapTypeSymbol(BuiltinTypes.String, NamedType("Value", "pkg"));
        var mapTwo = new MapTypeSymbol(BuiltinTypes.String, NamedType("Value", "pkg"));
        Assert.IsTrue(Comparer.Equals(mapOne, mapTwo));
    }

    [TestMethod]
    public void Equals_MapsWithDifferentValuePackage_ReturnsFalse()
    {
        var mapOne = new MapTypeSymbol(BuiltinTypes.String, NamedType("Value", "pkg/one"));
        var mapTwo = new MapTypeSymbol(BuiltinTypes.String, NamedType("Value", "pkg/two"));
        Assert.IsFalse(Comparer.Equals(mapOne, mapTwo));
    }

    [TestMethod]
    public void Equals_ChannelsWithEquivalentElement_ReturnsTrue()
    {
        var channelOne = new ChannelTypeSymbol(NamedType("Message", "pkg"));
        var channelTwo = new ChannelTypeSymbol(NamedType("Message", "pkg"));
        Assert.IsTrue(Comparer.Equals(channelOne, channelTwo));
    }

    [TestMethod]
    public void Equals_FunctionsWithSameSignature_ReturnsTrue()
    {
        var functionOne = new FunctionTypeSymbol(
            new TypeSymbol[] { BuiltinTypes.Int }, new TypeSymbol[] { BuiltinTypes.String });
        var functionTwo = new FunctionTypeSymbol(
            new TypeSymbol[] { BuiltinTypes.Int }, new TypeSymbol[] { BuiltinTypes.String });
        Assert.IsTrue(Comparer.Equals(functionOne, functionTwo));
    }

    [TestMethod]
    public void Equals_FunctionsDifferingOnlyByVariadic_ReturnsFalse()
    {
        var fixedArity = new FunctionTypeSymbol(
            new TypeSymbol[] { BuiltinTypes.Int }, Array.Empty<TypeSymbol>(), isVariadic: false);
        var variadic = new FunctionTypeSymbol(
            new TypeSymbol[] { BuiltinTypes.Int }, Array.Empty<TypeSymbol>(), isVariadic: true);
        Assert.IsFalse(Comparer.Equals(fixedArity, variadic));
    }

    [TestMethod]
    public void Equals_InstantiationsWithEquivalentArguments_ReturnsTrue()
    {
        var listOfInt = new InstantiatedTypeSymbol(
            NamedType("List", "container"), new TypeSymbol[] { BuiltinTypes.Int });
        var listOfIntAgain = new InstantiatedTypeSymbol(
            NamedType("List", "container"), new TypeSymbol[] { BuiltinTypes.Int });
        Assert.IsTrue(Comparer.Equals(listOfInt, listOfIntAgain));
    }

    [TestMethod]
    public void Equals_InstantiationsWithDifferentArguments_ReturnsFalse()
    {
        var genericType = NamedType("List", "container");
        var listOfInt = new InstantiatedTypeSymbol(genericType, new TypeSymbol[] { BuiltinTypes.Int });
        var listOfString = new InstantiatedTypeSymbol(genericType, new TypeSymbol[] { BuiltinTypes.String });
        Assert.IsFalse(Comparer.Equals(listOfInt, listOfString));
    }

    [TestMethod]
    public void Equals_TypeParametersWithSameNameAndOrdinal_ReturnsTrue()
    {
        var firstParameter = new TypeParameterSymbol("K", 0, ConstraintInfo.Any);
        var secondParameter = new TypeParameterSymbol("K", 0, ConstraintInfo.Any);
        Assert.IsTrue(Comparer.Equals(firstParameter, secondParameter));
    }

    [TestMethod]
    public void Equals_TypeParametersWithDifferentOrdinal_ReturnsFalse()
    {
        var ordinalZero = new TypeParameterSymbol("K", 0, ConstraintInfo.Any);
        var ordinalOne = new TypeParameterSymbol("K", 1, ConstraintInfo.Any);
        Assert.IsFalse(Comparer.Equals(ordinalZero, ordinalOne));
    }

    [TestMethod]
    public void Equals_PointerAndItsNamedElement_ReturnsFalse()
    {
        var foo = NamedType("Foo", "pkg");
        var pointerToFoo = new PointerTypeSymbol(foo);
        Assert.IsFalse(Comparer.Equals(pointerToFoo, foo));
    }

    [TestMethod]
    public void GetHashCode_EquivalentNamedTypes_AreEqual()
    {
        Assert.AreEqual(
            Comparer.GetHashCode(NamedType("Foo", "pkg")),
            Comparer.GetHashCode(NamedType("Foo", "pkg")));
    }

    [TestMethod]
    public void GetHashCode_EquivalentSlices_AreEqual()
    {
        Assert.AreEqual(
            Comparer.GetHashCode(new SliceTypeSymbol(NamedType("Foo", "pkg"))),
            Comparer.GetHashCode(new SliceTypeSymbol(NamedType("Foo", "pkg"))));
    }

    // The real cross-archive scenario: a type registered during one phase is looked up with a
    // distinct-but-equivalent instance re-materialized from a dependency.
    [TestMethod]
    public void Dictionary_LookupWithEquivalentButDistinctKey_FindsValue()
    {
        var registry = new Dictionary<TypeSymbol, string>(Comparer);
        registry[NamedType("Certificate", "crypto/x509")] = "GoCertificate";

        var lookupKey = NamedType("Certificate", "crypto/x509");
        Assert.IsTrue(registry.ContainsKey(lookupKey));
        Assert.AreEqual("GoCertificate", registry[lookupKey]);
    }

    [TestMethod]
    public void Dictionary_SameNameDifferentPackage_DoesNotCollide()
    {
        var registry = new Dictionary<TypeSymbol, string>(Comparer)
        {
            [NamedType("Config", "pkg/one")] = "one",
            [NamedType("Config", "pkg/two")] = "two",
        };

        Assert.AreEqual(2, registry.Count);
        Assert.AreEqual("one", registry[NamedType("Config", "pkg/one")]);
        Assert.AreEqual("two", registry[NamedType("Config", "pkg/two")]);
    }
}
