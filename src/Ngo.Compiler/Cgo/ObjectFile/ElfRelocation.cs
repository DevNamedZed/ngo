// -----------------------------------------------------------------------
// <copyright file="ElfRelocation.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// A single ELF64 RELA relocation entry. Describes the instruction
    /// "patch <c>target[OffsetInTargetSection]</c> with a value computed
    /// from relocation type <see cref="RelocationType"/>, symbol
    /// <see cref="SymbolIndex"/>, and addend <see cref="Addend"/>." The
    /// ELF reader parses one of these per row in each <c>.rela.*</c>
    /// section before feeding them through
    /// <see cref="ElfRelocationApplier"/>.
    /// </summary>
    public sealed class ElfRelocation
    {
        public ElfRelocation(
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
        /// Byte offset inside the target section where this relocation's
        /// computed value must be written (ELF64 <c>r_offset</c>). The
        /// applier validates that the resulting write stays inside the
        /// target section's byte buffer before touching it.
        /// </summary>
        public ulong OffsetInTargetSection { get; }

        /// <summary>
        /// Processor-specific relocation type — the low 32 bits of
        /// ELF64 <c>r_info</c>. For x86_64 these are the
        /// <c>R_X86_64_*</c> constants.
        /// </summary>
        public uint RelocationType { get; }

        /// <summary>
        /// Symbol table index — the high 32 bits of ELF64 <c>r_info</c>.
        /// Zero means there is no associated symbol and the relocation
        /// must be computed as if the symbol value were zero.
        /// </summary>
        public uint SymbolIndex { get; }

        /// <summary>
        /// Signed constant added to the symbol's value when computing
        /// the final relocated value (ELF64 <c>r_addend</c>). DWARF
        /// cross-references are almost always addend-only because the
        /// paired symbol is a section symbol whose value is zero.
        /// </summary>
        public long Addend { get; }
    }
}
