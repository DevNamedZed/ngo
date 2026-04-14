// -----------------------------------------------------------------------
// <copyright file="SyntheticCompilationUnit.cs" company="Ziad">
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
using Ngo.Compiler.Cgo.Dwarf;
using Ngo.Compiler.Tests.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Symbols;

/// <summary>
/// Test helper that drives the existing
/// <see cref="SyntheticAbbreviationTableBuilder"/> and
/// <see cref="SyntheticDebugInfoBuilder"/> to assemble a single
/// DWARF 4 / DWARF32 compilation unit with labelled DIE offsets.
/// The resolver tests need several DIEs per CU with cross-references
/// between them; the label book-keeping lives here so the test
/// bodies stay focused on the scenario they describe.
/// </summary>
internal sealed class SyntheticCompilationUnit
{
    private readonly SyntheticAbbreviationTableBuilder _abbreviationBuilder = new();
    private readonly SyntheticDebugInfoBuilder _debugInfoBuilder = new();
    private readonly Dictionary<string, int> _offsetByLabel = new();
    private int _nextAbbreviationCode = 1;
    private bool _compilationUnitStarted;
    private bool _compilationUnitFinalised;
    private DwarfCompilationUnit? _parsedCompilationUnit;

    public SyntheticAbbreviationTableBuilder AbbreviationBuilder
    {
        get { return _abbreviationBuilder; }
    }

    public SyntheticDebugInfoBuilder DebugInfoBuilder
    {
        get { return _debugInfoBuilder; }
    }

    public int DeclareAbbreviation(
        DwarfTag tag,
        bool hasChildren,
        IReadOnlyList<SyntheticAbbreviationAttribute> attributes)
    {
        int code = _nextAbbreviationCode++;
        _abbreviationBuilder.AppendAbbreviation(code, tag, hasChildren, attributes);
        return code;
    }

    public SyntheticCompilationUnit StartCompilationUnit()
    {
        _debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4,
            DwarfUnitFormat.Dwarf32,
            addressSize: 8,
            debugAbbrevOffset: 0);
        _compilationUnitStarted = true;
        return this;
    }

    public int LabelNextDie(string label)
    {
        int offset = _debugInfoBuilder.Position;
        _offsetByLabel[label] = offset;
        return offset;
    }

    public int GetDieOffset(string label)
    {
        return _offsetByLabel[label];
    }

    public SyntheticCompilationUnit AppendAbbreviationCode(int code)
    {
        _debugInfoBuilder.AppendUnsignedLeb128((ulong)code);
        return this;
    }

    public SyntheticCompilationUnit AppendNullDie()
    {
        _debugInfoBuilder.AppendUnsignedLeb128(0);
        return this;
    }

    public DwarfCompilationUnit Build()
    {
        if (!_compilationUnitStarted)
        {
            throw new System.InvalidOperationException(
                "StartCompilationUnit must be called before Build.");
        }
        if (!_compilationUnitFinalised)
        {
            _debugInfoBuilder.EndCompilationUnit();
            _abbreviationBuilder.AppendTableTerminator();
            _compilationUnitFinalised = true;
        }
        if (_parsedCompilationUnit == null)
        {
            DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
                _debugInfoBuilder.ToArray(),
                _abbreviationBuilder.ToArray(),
                null,
                null));
            _parsedCompilationUnit = debugInfo.CompilationUnits[0];
        }
        return _parsedCompilationUnit;
    }

    public DwarfDie GetDie(string label)
    {
        DwarfCompilationUnit compilationUnit = Build();
        int offset = GetDieOffset(label);
        return compilationUnit.DiesByOffsetInDebugInfo[offset];
    }
}
