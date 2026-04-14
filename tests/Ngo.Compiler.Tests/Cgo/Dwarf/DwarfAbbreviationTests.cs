// -----------------------------------------------------------------------
// <copyright file="DwarfAbbreviationTests.cs" company="Ziad">
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
/// Unit tests for the <see cref="DwarfAbbreviation"/> DTO. The DTO
/// holds no behaviour of its own but guards two invariants the rest
/// of the parser relies on: abbreviation codes are strictly positive
/// (the zero code is the table terminator) and the attribute list is
/// never null (the DIE walker iterates it without a null check). If
/// either invariant ever regresses, the parser would silently
/// produce corrupt DIE decodes — so the checks get a dedicated test
/// class rather than an implicit cover by the parser tests.
/// </summary>
[TestClass]
public class DwarfAbbreviationTests
{
    [TestMethod]
    public void Constructor_ZeroCode_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new DwarfAbbreviation(
                0,
                DwarfTag.BaseType,
                false,
                new List<DwarfAbbreviationAttribute>()));
    }

    [TestMethod]
    public void Constructor_NegativeCode_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new DwarfAbbreviation(
                -1,
                DwarfTag.BaseType,
                false,
                new List<DwarfAbbreviationAttribute>()));
    }

    [TestMethod]
    public void Constructor_NullAttributes_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new DwarfAbbreviation(1, DwarfTag.BaseType, false, null!));
    }

    [TestMethod]
    public void Constructor_ValidInputs_StoresFields()
    {
        List<DwarfAbbreviationAttribute> attributes = new()
        {
            new DwarfAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp, 0),
        };
        DwarfAbbreviation abbreviation = new(5, DwarfTag.StructureType, true, attributes);

        Assert.AreEqual(5, abbreviation.Code);
        Assert.AreEqual(DwarfTag.StructureType, abbreviation.Tag);
        Assert.IsTrue(abbreviation.HasChildren);
        Assert.AreSame(attributes, abbreviation.Attributes);
    }
}
