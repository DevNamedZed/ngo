// -----------------------------------------------------------------------
// <copyright file="SynthesizedElfRelocation.cs" company="Ziad">
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

namespace Ngo.Compiler.Tests.Cgo.ObjectFile;

/// <summary>
/// Description of a single relocation row the
/// <see cref="SyntheticElf64Builder"/> should emit into a synthesized
/// <c>.rela.*</c> section. Tests use one per call-site the reader is
/// expected to patch, then assert on the target section's bytes after
/// the reader has applied relocations.
/// </summary>
public sealed class SynthesizedElfRelocation
{
    public SynthesizedElfRelocation(
        ulong offsetInTargetSection,
        uint relocationType,
        uint symbolIndex,
        long addend)
    {
        OffsetInTargetSection = offsetInTargetSection;
        RelocationType = relocationType;
        SymbolIndex = symbolIndex;
        Addend = addend;
    }

    /// <summary>
    /// Byte offset inside the target section (<c>r_offset</c>).
    /// </summary>
    public ulong OffsetInTargetSection { get; }

    /// <summary>
    /// Processor-specific relocation type (low 32 bits of
    /// <c>r_info</c>). For x86_64 tests use the <c>R_X86_64_*</c>
    /// constants: 1 for 64-bit, 10 for 32-bit, 11 for 32-bit signed.
    /// </summary>
    public uint RelocationType { get; }

    /// <summary>
    /// Zero-based index into the symbol table the relocation should
    /// reference (high 32 bits of <c>r_info</c>). Index 0 is the
    /// undefined symbol and produces a zero symbol value.
    /// </summary>
    public uint SymbolIndex { get; }

    /// <summary>
    /// Signed constant the applier adds to the symbol value
    /// (<c>r_addend</c>).
    /// </summary>
    public long Addend { get; }
}
