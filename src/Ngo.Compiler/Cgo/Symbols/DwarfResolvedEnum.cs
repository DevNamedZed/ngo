// -----------------------------------------------------------------------
// <copyright file="DwarfResolvedEnum.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// Layout of a C <c>enum</c> as recovered from DWARF. The
    /// <see cref="IsSigned"/> flag comes from the underlying integer
    /// base type's <c>DW_AT_encoding</c> when present; GCC and clang
    /// default C enums to signed, but other front-ends may pick
    /// unsigned, so the reader always consults the base type rather
    /// than assuming.
    /// </summary>
    public sealed class DwarfResolvedEnum
    {
        public DwarfResolvedEnum(
            string? name,
            long sizeBytes,
            long alignmentBytes,
            bool isSigned,
            IReadOnlyList<DwarfResolvedEnumerator> enumerators)
        {
            if (enumerators == null)
            {
                throw new ArgumentNullException(nameof(enumerators));
            }
            if (sizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeBytes), "Enum size cannot be negative.");
            }
            if (alignmentBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alignmentBytes), "Enum alignment cannot be negative.");
            }

            Name = name;
            SizeBytes = sizeBytes;
            AlignmentBytes = alignmentBytes;
            IsSigned = isSigned;
            Enumerators = enumerators;
        }

        public string? Name { get; }

        public long SizeBytes { get; }

        public long AlignmentBytes { get; }

        public bool IsSigned { get; }

        public IReadOnlyList<DwarfResolvedEnumerator> Enumerators { get; }
    }
}
