// -----------------------------------------------------------------------
// <copyright file="DwarfCompilationUnit.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// One DWARF compilation unit. A <c>.debug_info</c> section
    /// holds a sequence of these, each with its own CU header,
    /// abbreviation table reference, address size, and DIE tree.
    /// Cross-CU references (<see cref="DwarfForm.RefAddr"/>) are
    /// resolved using the absolute offsets recorded on every DIE,
    /// so the Layer-4 type resolver can walk across CUs without
    /// holding per-CU state.
    /// </summary>
    public sealed class DwarfCompilationUnit
    {
        public DwarfCompilationUnit(
            int version,
            int addressSize,
            DwarfUnitFormat unitFormat,
            int headerOffsetInDebugInfo,
            int debugAbbrevOffset,
            string? name,
            string? compDir,
            string? producer,
            DwarfLanguage language,
            IReadOnlyDictionary<int, DwarfDie> diesByOffsetInDebugInfo,
            IReadOnlyList<DwarfDie> topLevelDies)
        {
            if (diesByOffsetInDebugInfo == null)
            {
                throw new ArgumentNullException(nameof(diesByOffsetInDebugInfo));
            }
            if (topLevelDies == null)
            {
                throw new ArgumentNullException(nameof(topLevelDies));
            }

            Version = version;
            AddressSize = addressSize;
            UnitFormat = unitFormat;
            HeaderOffsetInDebugInfo = headerOffsetInDebugInfo;
            DebugAbbrevOffset = debugAbbrevOffset;
            Name = name;
            CompDir = compDir;
            Producer = producer;
            Language = language;
            DiesByOffsetInDebugInfo = diesByOffsetInDebugInfo;
            TopLevelDies = topLevelDies;
        }

        public int Version { get; }

        /// <summary>
        /// Size in bytes of a target address for DIEs in this CU.
        /// Used to decode <see cref="DwarfForm.Addr"/>. ELF64 anchor
        /// probes always produce <c>8</c>; the field is here for
        /// completeness and future 32-bit targets.
        /// </summary>
        public int AddressSize { get; }

        public DwarfUnitFormat UnitFormat { get; }

        /// <summary>
        /// Absolute byte offset of the CU header's first byte inside
        /// <c>.debug_info</c>. Used to convert CU-relative
        /// <see cref="DwarfForm.Ref1"/>/<see cref="DwarfForm.Ref2"/>/etc.
        /// references to absolute offsets.
        /// </summary>
        public int HeaderOffsetInDebugInfo { get; }

        public int DebugAbbrevOffset { get; }

        public string? Name { get; }

        public string? CompDir { get; }

        public string? Producer { get; }

        public DwarfLanguage Language { get; }

        public IReadOnlyDictionary<int, DwarfDie> DiesByOffsetInDebugInfo { get; }

        public IReadOnlyList<DwarfDie> TopLevelDies { get; }
    }
}
