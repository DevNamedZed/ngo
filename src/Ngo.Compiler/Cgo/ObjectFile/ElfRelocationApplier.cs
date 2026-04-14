// -----------------------------------------------------------------------
// <copyright file="ElfRelocationApplier.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// Applies x86_64 RELA relocations to a target section's bytes in
    /// place. Anchor probe <c>.o</c> files produced by gcc/clang with
    /// <c>-g</c> leave every DWARF cross-reference offset zeroed in
    /// the raw bytes and supply the real values in companion
    /// <c>.rela.debug_*</c> sections, so the DWARF reader sees zeros
    /// everywhere — all string names read back as the first entry of
    /// <c>.debug_str</c> — unless the relocations are applied first.
    ///
    /// Only the relocation types that gcc and clang emit for DWARF
    /// cross-references are accepted: <c>R_X86_64_64</c>,
    /// <c>R_X86_64_32</c>, and <c>R_X86_64_32S</c>
    /// (plus the no-op <c>R_X86_64_NONE</c>). Any other type triggers
    /// <see cref="ObjectFileException"/> so malformed or non-x86_64
    /// input never silently corrupts the debug bytes we hand to the
    /// DWARF reader.
    /// </summary>
    internal static class ElfRelocationApplier
    {
        private const uint RelocationTypeNone = 0;
        private const uint RelocationType64 = 1;
        private const uint RelocationType32 = 10;
        private const uint RelocationType32Signed = 11;

        /// <summary>
        /// Apply every entry of <paramref name="relocations"/> to
        /// <paramref name="targetSectionBytes"/> in place. The symbol
        /// table referenced by those entries must already be fully
        /// parsed and supplied as <paramref name="symbolTable"/>.
        /// </summary>
        public static void Apply(
            byte[] targetSectionBytes,
            string targetSectionName,
            IReadOnlyList<ElfRelocation> relocations,
            IReadOnlyList<ElfSymbol> symbolTable,
            string objectFilePath)
        {
            if (targetSectionBytes == null)
            {
                throw new ArgumentNullException(nameof(targetSectionBytes));
            }
            if (targetSectionName == null)
            {
                throw new ArgumentNullException(nameof(targetSectionName));
            }
            if (relocations == null)
            {
                throw new ArgumentNullException(nameof(relocations));
            }
            if (symbolTable == null)
            {
                throw new ArgumentNullException(nameof(symbolTable));
            }

            foreach (ElfRelocation relocation in relocations)
            {
                ApplyOne(
                    targetSectionBytes, targetSectionName,
                    relocation, symbolTable, objectFilePath);
            }
        }

        private static void ApplyOne(
            byte[] targetSectionBytes,
            string targetSectionName,
            ElfRelocation relocation,
            IReadOnlyList<ElfSymbol> symbolTable,
            string objectFilePath)
        {
            if (relocation.RelocationType == RelocationTypeNone)
            {
                return;
            }

            ulong symbolValue = ResolveSymbolValue(
                relocation, symbolTable, targetSectionName, objectFilePath);
            long combinedValue = unchecked((long)symbolValue + relocation.Addend);

            switch (relocation.RelocationType)
            {
                case RelocationType64:
                {
                    WriteUInt64LittleEndian(
                        targetSectionBytes,
                        relocation.OffsetInTargetSection,
                        unchecked((ulong)combinedValue),
                        targetSectionName,
                        relocation.RelocationType,
                        objectFilePath);
                    return;
                }
                case RelocationType32:
                case RelocationType32Signed:
                {
                    WriteUInt32LittleEndian(
                        targetSectionBytes,
                        relocation.OffsetInTargetSection,
                        unchecked((uint)combinedValue),
                        targetSectionName,
                        relocation.RelocationType,
                        objectFilePath);
                    return;
                }
                default:
                {
                    throw new ObjectFileException(
                        "Unsupported x86_64 relocation type " + relocation.RelocationType +
                        " in section " + targetSectionName + " at target offset " +
                        relocation.OffsetInTargetSection + ". This stage only handles " +
                        "R_X86_64_NONE (0), R_X86_64_64 (1), R_X86_64_32 (10), and " +
                        "R_X86_64_32S (11); all DWARF cross-references from gcc/clang " +
                        "anchor probes must fall into that set.",
                        objectFilePath);
                }
            }
        }

        private static ulong ResolveSymbolValue(
            ElfRelocation relocation,
            IReadOnlyList<ElfSymbol> symbolTable,
            string targetSectionName,
            string objectFilePath)
        {
            if (relocation.SymbolIndex == 0)
            {
                return 0;
            }

            if (relocation.SymbolIndex >= (uint)symbolTable.Count)
            {
                throw new ObjectFileException(
                    "Relocation in section " + targetSectionName +
                    " references symbol index " + relocation.SymbolIndex +
                    " but the symbol table only has " + symbolTable.Count + " entries.",
                    objectFilePath);
            }

            return symbolTable[(int)relocation.SymbolIndex].Value;
        }

        private static void WriteUInt32LittleEndian(
            byte[] target,
            ulong offsetInSection,
            uint value,
            string targetSectionName,
            uint relocationType,
            string objectFilePath)
        {
            EnsureRangeInSection(
                target, offsetInSection, length: 4,
                targetSectionName, relocationType, objectFilePath);
            int writeOffset = checked((int)offsetInSection);
            target[writeOffset + 0] = (byte)(value & 0xFF);
            target[writeOffset + 1] = (byte)((value >> 8) & 0xFF);
            target[writeOffset + 2] = (byte)((value >> 16) & 0xFF);
            target[writeOffset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteUInt64LittleEndian(
            byte[] target,
            ulong offsetInSection,
            ulong value,
            string targetSectionName,
            uint relocationType,
            string objectFilePath)
        {
            EnsureRangeInSection(
                target, offsetInSection, length: 8,
                targetSectionName, relocationType, objectFilePath);
            int writeOffset = checked((int)offsetInSection);
            target[writeOffset + 0] = (byte)(value & 0xFF);
            target[writeOffset + 1] = (byte)((value >> 8) & 0xFF);
            target[writeOffset + 2] = (byte)((value >> 16) & 0xFF);
            target[writeOffset + 3] = (byte)((value >> 24) & 0xFF);
            target[writeOffset + 4] = (byte)((value >> 32) & 0xFF);
            target[writeOffset + 5] = (byte)((value >> 40) & 0xFF);
            target[writeOffset + 6] = (byte)((value >> 48) & 0xFF);
            target[writeOffset + 7] = (byte)((value >> 56) & 0xFF);
        }

        private static void EnsureRangeInSection(
            byte[] target,
            ulong offsetInSection,
            uint length,
            string targetSectionName,
            uint relocationType,
            string objectFilePath)
        {
            ulong sectionLength = (ulong)target.Length;
            ulong endOffset;
            try
            {
                endOffset = checked(offsetInSection + length);
            }
            catch (OverflowException overflow)
            {
                throw new ObjectFileException(
                    "Relocation (type " + relocationType + ") in section " + targetSectionName +
                    " at target offset " + offsetInSection + " with write length " + length +
                    " overflows 64-bit arithmetic.",
                    objectFilePath,
                    overflow);
            }
            if (endOffset > sectionLength)
            {
                throw new ObjectFileException(
                    "Relocation (type " + relocationType + ") in section " + targetSectionName +
                    " writes bytes [" + offsetInSection + ", " + endOffset +
                    ") past end of section (" + sectionLength + " bytes).",
                    objectFilePath);
            }
        }
    }
}
