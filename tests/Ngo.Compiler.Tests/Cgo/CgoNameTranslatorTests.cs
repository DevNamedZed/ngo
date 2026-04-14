// -----------------------------------------------------------------------
// <copyright file="CgoNameTranslatorTests.cs" company="Ziad">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;

namespace Ngo.Compiler.Tests.Cgo;

/// <summary>
/// Unit tests for <see cref="CgoNameTranslator"/>. These tests lock
/// in the Go-to-C tag translation so that the anchor probe emits
/// legal C expressions (e.g. <c>sizeof(struct foo)</c>) rather than
/// Go-only identifiers (<c>sizeof(struct_foo)</c>) that the C
/// compiler rejects with confusing <c>undeclared here</c> errors.
/// </summary>
[TestClass]
public class CgoNameTranslatorTests
{
    [TestMethod]
    public void StructPrefix_TranslatedToStructKeyword()
    {
        Assert.AreEqual("struct foo", CgoNameTranslator.ToCExpression("struct_foo"));
        Assert.AreEqual(
            "struct ZSTD_CDict_s",
            CgoNameTranslator.ToCExpression("struct_ZSTD_CDict_s"));
    }

    [TestMethod]
    public void UnionPrefix_TranslatedToUnionKeyword()
    {
        Assert.AreEqual("union variant", CgoNameTranslator.ToCExpression("union_variant"));
    }

    [TestMethod]
    public void EnumPrefix_TranslatedToEnumKeyword()
    {
        Assert.AreEqual("enum color", CgoNameTranslator.ToCExpression("enum_color"));
    }

    [TestMethod]
    public void PlainTypedefOrFunctionName_PassesThroughUnchanged()
    {
        Assert.AreEqual("sqlite3_open", CgoNameTranslator.ToCExpression("sqlite3_open"));
        Assert.AreEqual("size_t", CgoNameTranslator.ToCExpression("size_t"));
        Assert.AreEqual("ZSTD_CCtx", CgoNameTranslator.ToCExpression("ZSTD_CCtx"));
    }

    [TestMethod]
    public void PrefixOnlyWithNoTagName_TranslatedWithEmptyTag()
    {
        // Bare "struct_" / "union_" / "enum_" are not valid Go cgo
        // identifiers in practice, but the translator stays total so
        // that callers never get null or an exception. The emitted C
        // ("struct ") then fails at the C compile step with a clear
        // diagnostic rather than being silently dropped here.
        Assert.AreEqual("struct ", CgoNameTranslator.ToCExpression("struct_"));
        Assert.AreEqual("union ", CgoNameTranslator.ToCExpression("union_"));
        Assert.AreEqual("enum ", CgoNameTranslator.ToCExpression("enum_"));
    }

    [TestMethod]
    public void NameContainingPrefixInMiddle_NotTranslated()
    {
        Assert.AreEqual(
            "my_struct_foo",
            CgoNameTranslator.ToCExpression("my_struct_foo"));
    }

    [TestMethod]
    public void EmptyString_PassesThroughUnchanged()
    {
        Assert.AreEqual(string.Empty, CgoNameTranslator.ToCExpression(string.Empty));
    }
}
