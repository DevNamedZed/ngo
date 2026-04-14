// -----------------------------------------------------------------------
// <copyright file="SynthesizedElfSymbol.cs" company="Ziad">
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

namespace Ngo.Compiler.Tests.Cgo.ObjectFile;

/// <summary>
/// Description of a single symbol row the
/// <see cref="SyntheticElf64Builder"/> should emit into a synthesized
/// <c>SHT_SYMTAB</c> section. Tests use this to build the symbol table
/// their relocation entries will index into.
/// </summary>
public sealed class SynthesizedElfSymbol
{
    public SynthesizedElfSymbol(
        string name,
        byte info,
        ulong value,
        ulong size,
        string? definingSectionName)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        Name = name;
        Info = info;
        Value = value;
        Size = size;
        DefiningSectionName = definingSectionName;
    }

    /// <summary>
    /// Symbol name as it should appear in the companion string table.
    /// Empty means <c>st_name = 0</c> (no associated name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// ELF64 <c>st_info</c> byte — <c>(binding &lt;&lt; 4) | type</c>.
    /// Section symbols use type 3 (<c>STT_SECTION</c>).
    /// </summary>
    public byte Info { get; }

    /// <summary>
    /// Symbol value (<c>st_value</c>). Zero for section symbols in
    /// relocatable objects.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    /// Symbol size (<c>st_size</c>). Usually zero for section symbols.
    /// </summary>
    public ulong Size { get; }

    /// <summary>
    /// Name of the section the symbol is defined in. The builder
    /// resolves this to a section index at <see cref="SyntheticElf64Builder.Build"/>
    /// time and stores it in <c>st_shndx</c>. <c>null</c> means the
    /// symbol is undefined (<c>SHN_UNDEF</c>, index 0).
    /// </summary>
    public string? DefiningSectionName { get; }
}
