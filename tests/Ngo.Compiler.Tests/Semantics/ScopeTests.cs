// -----------------------------------------------------------------------
// <copyright file="ScopeTests.cs" company="Ziad">
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
public class ScopeTests
{
    [TestMethod]
    public void Declare_and_lookup_returns_symbol()
    {
        var scope = new Scope("test", null);
        var symbol = new LocalSymbol("x", BuiltinTypes.Int);
        Assert.IsTrue(scope.TryDeclare(symbol));
        Assert.AreSame(symbol, scope.Lookup("x"));
    }

    [TestMethod]
    public void Declare_duplicate_returns_false()
    {
        var scope = new Scope("test", null);
        Assert.IsTrue(scope.TryDeclare(new LocalSymbol("x", BuiltinTypes.Int)));
        Assert.IsFalse(scope.TryDeclare(new LocalSymbol("x", BuiltinTypes.String)));
    }

    [TestMethod]
    public void Lookup_walks_parent_chain()
    {
        var parent = new Scope("parent", null);
        var child = new Scope("child", parent);
        var symbol = new LocalSymbol("x", BuiltinTypes.Int);
        parent.TryDeclare(symbol);
        Assert.AreSame(symbol, child.Lookup("x"));
    }

    [TestMethod]
    public void Lookup_returns_null_for_undeclared()
    {
        var scope = new Scope("test", null);
        Assert.IsNull(scope.Lookup("x"));
    }

    [TestMethod]
    public void LookupLocal_does_not_walk_parent()
    {
        var parent = new Scope("parent", null);
        var child = new Scope("child", parent);
        parent.TryDeclare(new LocalSymbol("x", BuiltinTypes.Int));
        Assert.IsNull(child.LookupLocal("x"));
    }

    [TestMethod]
    public void LookupLocal_finds_in_current_scope()
    {
        var scope = new Scope("test", null);
        var symbol = new LocalSymbol("x", BuiltinTypes.Int);
        scope.TryDeclare(symbol);
        Assert.AreSame(symbol, scope.LookupLocal("x"));
    }

    [TestMethod]
    public void Child_shadows_parent_symbol()
    {
        var parent = new Scope("parent", null);
        var child = new Scope("child", parent);
        var parentSym = new LocalSymbol("x", BuiltinTypes.Int);
        var childSym = new LocalSymbol("x", BuiltinTypes.String);
        parent.TryDeclare(parentSym);
        child.TryDeclare(childSym);
        Assert.AreSame(childSym, child.Lookup("x"));
        Assert.AreSame(parentSym, parent.Lookup("x"));
    }

    [TestMethod]
    public void Scope_has_name_and_parent()
    {
        var parent = new Scope("universe", null);
        var child = new Scope("package", parent);
        Assert.AreEqual("universe", parent.Name);
        Assert.AreEqual("package", child.Name);
        Assert.IsNull(parent.Parent);
        Assert.AreSame(parent, child.Parent);
    }

    [TestMethod]
    public void Three_level_scope_chain()
    {
        var universe = new Scope("universe", null);
        var pkg = new Scope("package", universe);
        var fn = new Scope("function", pkg);

        var typeSym = BuiltinTypes.Int;
        universe.TryDeclare(typeSym);

        var funcSym = new FunctionSymbol("add", System.Array.Empty<ParameterSymbol>(), BuiltinTypes.Void);
        pkg.TryDeclare(funcSym);

        var localSym = new LocalSymbol("x", BuiltinTypes.Int);
        fn.TryDeclare(localSym);

        // Function scope can see all three levels
        Assert.AreSame(localSym, fn.Lookup("x"));
        Assert.AreSame(funcSym, fn.Lookup("add"));
        Assert.AreSame(typeSym, fn.Lookup("int"));

        // Package scope can't see function locals
        Assert.IsNull(pkg.Lookup("x"));
        Assert.AreSame(funcSym, pkg.Lookup("add"));
    }
}
