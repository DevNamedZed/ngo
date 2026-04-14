// -----------------------------------------------------------------------
// <copyright file="DwarfTypeResolverEnumTests.cs" company="Ziad">
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
using Ngo.Compiler.Cgo.Dwarf;
using Ngo.Compiler.Cgo.Symbols;
using Ngo.Compiler.Tests.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Symbols;

/// <summary>
/// Exercises <see cref="DwarfTypeResolver.ResolveEnum"/>. Coverage
/// targets: enumerators returned in declaration order, signed vs
/// unsigned detection via the underlying base-type encoding,
/// signedness inference when the base type is reached through a
/// typedef chain, anonymous enumerations, and the error paths that
/// surface as <see cref="CgoDebugInfoException"/>: non-enumeration
/// tags, missing byte size, missing enumerator name, and missing
/// enumerator const value.
/// </summary>
[TestClass]
public class DwarfTypeResolverEnumTests
{
    [TestMethod]
    public void ResolveEnum_NamedEnumWithSignedBase_ReturnsEnumeratorsInOrder()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "Color",
            baseTypeEncoding: DwarfTypeEncoding.Signed,
            enumeratorsReference: "baseType",
            enumerators: new[]
            {
                new EnumeratorSpec("Red", 0),
                new EnumeratorSpec("Green", 1),
                new EnumeratorSpec("Blue", 2),
            });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.AreEqual("Color", resolved.Name);
        Assert.AreEqual(4L, resolved.SizeBytes);
        Assert.IsTrue(resolved.IsSigned);
        Assert.AreEqual(3, resolved.Enumerators.Count);
        Assert.AreEqual("Red", resolved.Enumerators[0].Name);
        Assert.AreEqual(0L, resolved.Enumerators[0].Value);
        Assert.AreEqual("Green", resolved.Enumerators[1].Name);
        Assert.AreEqual(1L, resolved.Enumerators[1].Value);
        Assert.AreEqual("Blue", resolved.Enumerators[2].Name);
        Assert.AreEqual(2L, resolved.Enumerators[2].Value);
    }

    [TestMethod]
    public void ResolveEnum_UnsignedBaseType_ReportsUnsigned()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "Flags",
            baseTypeEncoding: DwarfTypeEncoding.Unsigned,
            enumeratorsReference: "baseType",
            enumerators: new[]
            {
                new EnumeratorSpec("None", 0),
                new EnumeratorSpec("ReadBit", 1),
                new EnumeratorSpec("WriteBit", 2),
            });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.IsFalse(resolved.IsSigned);
        Assert.AreEqual(3, resolved.Enumerators.Count);
    }

    [TestMethod]
    public void ResolveEnum_SignedCharBase_ReportsSigned()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "TinyEnum",
            baseTypeEncoding: DwarfTypeEncoding.SignedChar,
            enumeratorsReference: "baseType",
            enumerators: new[]
            {
                new EnumeratorSpec("Alpha", 0),
            });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.IsTrue(resolved.IsSigned);
    }

    [TestMethod]
    public void ResolveEnum_UnsignedCharBase_ReportsUnsigned()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "CharEnum",
            baseTypeEncoding: DwarfTypeEncoding.UnsignedChar,
            enumeratorsReference: "baseType",
            enumerators: new[]
            {
                new EnumeratorSpec("Mark", 0),
            });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.IsFalse(resolved.IsSigned);
    }

    [TestMethod]
    public void ResolveEnum_BaseTypeBehindTypedef_UnwrapsAndDetectsSignedness()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "Level",
            baseTypeEncoding: DwarfTypeEncoding.Signed,
            enumeratorsReference: "typedef",
            enumerators: new[]
            {
                new EnumeratorSpec("Low", -1),
                new EnumeratorSpec("High", 1),
            },
            includeTypedef: true);
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.IsTrue(resolved.IsSigned);
        Assert.AreEqual(2, resolved.Enumerators.Count);
        Assert.AreEqual("Low", resolved.Enumerators[0].Name);
        Assert.AreEqual(-1L, resolved.Enumerators[0].Value);
        Assert.AreEqual("High", resolved.Enumerators[1].Name);
        Assert.AreEqual(1L, resolved.Enumerators[1].Value);
    }

    [TestMethod]
    public void ResolveEnum_WithoutTypeReference_DefaultsToSigned()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int enumAbbrev = unit.DeclareAbbreviation(
            DwarfTag.EnumerationType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int enumeratorAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Enumerator,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ConstValue, DwarfForm.Sdata),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("enum");
        unit.AppendAbbreviationCode(enumAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("UntypedEnum");
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(enumeratorAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("Only");
        unit.DebugInfoBuilder.AppendSignedLeb128(7);
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.IsTrue(resolved.IsSigned);
        Assert.AreEqual("UntypedEnum", resolved.Name);
        Assert.AreEqual(1, resolved.Enumerators.Count);
        Assert.AreEqual(7L, resolved.Enumerators[0].Value);
    }

    [TestMethod]
    public void ResolveEnum_AnonymousEnum_ReturnsNullName()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
                new(DwarfAttribute.Encoding, DwarfForm.Data1),
            });
        int enumAbbrev = unit.DeclareAbbreviation(
            DwarfTag.EnumerationType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int enumeratorAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Enumerator,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ConstValue, DwarfForm.Sdata),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.DebugInfoBuilder.AppendU8((byte)DwarfTypeEncoding.Signed);
        unit.LabelNextDie("enum");
        unit.AppendAbbreviationCode(enumAbbrev);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(enumeratorAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("Only");
        unit.DebugInfoBuilder.AppendSignedLeb128(42);
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.IsNull(resolved.Name);
        Assert.AreEqual(1, resolved.Enumerators.Count);
        Assert.AreEqual(42L, resolved.Enumerators[0].Value);
    }

    [TestMethod]
    public void ResolveEnum_EmptyEnumeratorList_ReturnsEmpty()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "Empty",
            baseTypeEncoding: DwarfTypeEncoding.Signed,
            enumeratorsReference: "baseType",
            enumerators: System.Array.Empty<EnumeratorSpec>());
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.AreEqual("Empty", resolved.Name);
        Assert.AreEqual(0, resolved.Enumerators.Count);
    }

    [TestMethod]
    public void ResolveEnum_OnStructureTypeTag_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int structAbbrev = unit.DeclareAbbreviation(
            DwarfTag.StructureType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("struct");
        unit.AppendAbbreviationCode(structAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("NotAnEnum");
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveEnum(unit.GetDie("struct")));
        StringAssert.Contains(exception.Message, "EnumerationType");
    }

    [TestMethod]
    public void ResolveEnum_MissingByteSize_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int enumAbbrev = unit.DeclareAbbreviation(
            DwarfTag.EnumerationType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("enum");
        unit.AppendAbbreviationCode(enumAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("SizelessEnum");
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveEnum(unit.GetDie("enum")));
        StringAssert.Contains(exception.Message, "DW_AT_byte_size");
    }

    [TestMethod]
    public void ResolveEnum_EnumeratorWithoutName_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int enumAbbrev = unit.DeclareAbbreviation(
            DwarfTag.EnumerationType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int enumeratorAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Enumerator,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ConstValue, DwarfForm.Sdata),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("enum");
        unit.AppendAbbreviationCode(enumAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("BrokenEnum");
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(enumeratorAbbrev);
        unit.DebugInfoBuilder.AppendSignedLeb128(1);
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveEnum(unit.GetDie("enum")));
        StringAssert.Contains(exception.Message, "DW_AT_name");
    }

    [TestMethod]
    public void ResolveEnum_EnumeratorWithoutConstValue_Throws()
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int enumAbbrev = unit.DeclareAbbreviation(
            DwarfTag.EnumerationType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int enumeratorAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Enumerator,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("enum");
        unit.AppendAbbreviationCode(enumAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("BrokenEnum");
        unit.DebugInfoBuilder.AppendU8(4);
        unit.AppendAbbreviationCode(enumeratorAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8("ValueMissing");
        unit.AppendNullDie();
        unit.AppendNullDie();

        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => resolver.ResolveEnum(unit.GetDie("enum")));
        StringAssert.Contains(exception.Message, "DW_AT_const_value");
        StringAssert.Contains(exception.Message, "ValueMissing");
    }

    [TestMethod]
    public void ResolveEnum_SignedNegativeEnumeratorValue_PreservesSign()
    {
        SyntheticCompilationUnit unit = BuildEnum(
            enumName: "Polarity",
            baseTypeEncoding: DwarfTypeEncoding.Signed,
            enumeratorsReference: "baseType",
            enumerators: new[]
            {
                new EnumeratorSpec("Negative", -42),
                new EnumeratorSpec("Zero", 0),
                new EnumeratorSpec("Positive", 42),
            });
        DwarfCompilationUnit compilationUnit = unit.Build();
        DwarfTypeResolver resolver = new(compilationUnit);

        DwarfResolvedEnum resolved = resolver.ResolveEnum(unit.GetDie("enum"));

        Assert.AreEqual(-42L, resolved.Enumerators[0].Value);
        Assert.AreEqual(0L, resolved.Enumerators[1].Value);
        Assert.AreEqual(42L, resolved.Enumerators[2].Value);
    }

    private sealed class EnumeratorSpec
    {
        public EnumeratorSpec(string name, long value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public long Value { get; }
    }

    private static SyntheticCompilationUnit BuildEnum(
        string enumName,
        DwarfTypeEncoding baseTypeEncoding,
        string enumeratorsReference,
        IReadOnlyList<EnumeratorSpec> enumerators,
        bool includeTypedef = false)
    {
        SyntheticCompilationUnit unit = new();
        int compileUnitAbbrev = unit.DeclareAbbreviation(
            DwarfTag.CompileUnit, hasChildren: true, new List<SyntheticAbbreviationAttribute>());
        int baseTypeAbbrev = unit.DeclareAbbreviation(
            DwarfTag.BaseType,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
                new(DwarfAttribute.Encoding, DwarfForm.Data1),
            });
        int typedefAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Typedef,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Type, DwarfForm.Ref4),
            });
        int enumAbbrev = unit.DeclareAbbreviation(
            DwarfTag.EnumerationType,
            hasChildren: true,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.Type, DwarfForm.Ref4),
                new(DwarfAttribute.ByteSize, DwarfForm.Data1),
            });
        int enumeratorAbbrev = unit.DeclareAbbreviation(
            DwarfTag.Enumerator,
            hasChildren: false,
            new List<SyntheticAbbreviationAttribute>
            {
                new(DwarfAttribute.Name, DwarfForm.String),
                new(DwarfAttribute.ConstValue, DwarfForm.Sdata),
            });

        unit.StartCompilationUnit();
        unit.AppendAbbreviationCode(compileUnitAbbrev);
        unit.LabelNextDie("baseType");
        unit.AppendAbbreviationCode(baseTypeAbbrev);
        unit.DebugInfoBuilder.AppendU8(4);
        unit.DebugInfoBuilder.AppendU8((byte)baseTypeEncoding);
        if (includeTypedef)
        {
            unit.LabelNextDie("typedef");
            unit.AppendAbbreviationCode(typedefAbbrev);
            unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset("baseType"));
        }
        unit.LabelNextDie("enum");
        unit.AppendAbbreviationCode(enumAbbrev);
        unit.DebugInfoBuilder.AppendNullTerminatedUtf8(enumName);
        unit.DebugInfoBuilder.AppendU32((uint)unit.GetDieOffset(enumeratorsReference));
        unit.DebugInfoBuilder.AppendU8(4);
        foreach (EnumeratorSpec enumerator in enumerators)
        {
            unit.AppendAbbreviationCode(enumeratorAbbrev);
            unit.DebugInfoBuilder.AppendNullTerminatedUtf8(enumerator.Name);
            unit.DebugInfoBuilder.AppendSignedLeb128(enumerator.Value);
        }
        unit.AppendNullDie();
        unit.AppendNullDie();
        return unit;
    }
}
