// -----------------------------------------------------------------------
// <copyright file="SymbolEqualityComparerTests.cs" company="Ziad">
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
public class SymbolEqualityComparerTests
{
    private static readonly SymbolEqualityComparer Comparer = SymbolEqualityComparer.Instance;

    private static StructTypeSymbol Receiver(string name, string package)
    {
        return new StructTypeSymbol(name, Array.Empty<FieldSymbol>(), package);
    }

    private static MethodSymbol Method(string name, StructTypeSymbol receiver, bool isPointerReceiver)
    {
        return new MethodSymbol(name, receiver, isPointerReceiver,
            Array.Empty<ParameterSymbol>(), BuiltinTypes.Void);
    }

    private static FunctionSymbol Function(string name, string package)
    {
        return new FunctionSymbol(name, Array.Empty<ParameterSymbol>(),
            Array.Empty<TypeSymbol>(), isVariadic: false, packageName: package);
    }

    [TestMethod]
    public void Equals_MethodsWithSameNameAndReceiverIdentity_ReturnsTrue()
    {
        var methodOne = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        var methodTwo = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        Assert.IsTrue(Comparer.Equals(methodOne, methodTwo));
    }

    [TestMethod]
    public void Equals_MethodsWithSameNameDifferentReceiverPackage_ReturnsFalse()
    {
        var methodInBytes = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        var methodInStrings = Method("WriteByte", Receiver("Buffer", "strings"), isPointerReceiver: true);
        Assert.IsFalse(Comparer.Equals(methodInBytes, methodInStrings));
    }

    [TestMethod]
    public void Equals_MethodsDifferingByPointerReceiver_ReturnsFalse()
    {
        var pointerReceiverMethod = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        var valueReceiverMethod = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: false);
        Assert.IsFalse(Comparer.Equals(pointerReceiverMethod, valueReceiverMethod));
    }

    [TestMethod]
    public void Equals_FunctionsWithSameNameAndPackage_ReturnsTrue()
    {
        Assert.IsTrue(Comparer.Equals(Function("Sprintf", "fmt"), Function("Sprintf", "fmt")));
    }

    [TestMethod]
    public void Equals_FunctionsWithDifferentPackage_ReturnsFalse()
    {
        Assert.IsFalse(Comparer.Equals(Function("Sprintf", "fmt"), Function("Sprintf", "log")));
    }

    [TestMethod]
    public void Equals_SymbolsOfDifferentKind_ReturnsFalse()
    {
        var function = Function("Buffer", "bytes");
        var method = Method("Buffer", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        Assert.IsFalse(Comparer.Equals(function, method));
    }

    [TestMethod]
    public void Equals_TypeSymbols_DelegatesToTypeComparer()
    {
        var typeOne = new StructTypeSymbol("Foo", Array.Empty<FieldSymbol>(), "pkg");
        var typeTwo = new StructTypeSymbol("Foo", Array.Empty<FieldSymbol>(), "pkg");
        Assert.IsTrue(Comparer.Equals(typeOne, typeTwo));

        var typeOtherPackage = new StructTypeSymbol("Foo", Array.Empty<FieldSymbol>(), "other");
        Assert.IsFalse(Comparer.Equals(typeOne, typeOtherPackage));
    }

    [TestMethod]
    public void GetHashCode_EquivalentMethods_AreEqual()
    {
        var methodOne = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        var methodTwo = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        Assert.AreEqual(Comparer.GetHashCode(methodOne), Comparer.GetHashCode(methodTwo));
    }

    [TestMethod]
    public void Dictionary_MethodLookupWithEquivalentKey_FindsValue()
    {
        var registry = new Dictionary<Symbol, string>(Comparer);
        registry[Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true)] = "il";

        var lookupKey = Method("WriteByte", Receiver("Buffer", "bytes"), isPointerReceiver: true);
        Assert.IsTrue(registry.ContainsKey(lookupKey));
        Assert.AreEqual("il", registry[lookupKey]);
    }
}
