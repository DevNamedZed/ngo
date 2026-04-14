// -----------------------------------------------------------------------
// <copyright file="DwarfResolvedStructLayout.cs" company="Ziad">
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
    /// Layout of a C <c>struct</c> or <c>union</c> as recovered from
    /// DWARF. <see cref="IsOpaque"/> distinguishes declaration-only
    /// DIEs — where the C header exposed a forward-declared tag
    /// without a body — from fully-defined composites. An opaque
    /// layout has no fields and a zero size/alignment; callers must
    /// not attempt to marshal an opaque type by value.
    ///
    /// <see cref="AlignmentBytes"/> is zero when DWARF did not emit
    /// <c>DW_AT_alignment</c>; GCC often omits it and the consumer
    /// (Layer 4 symbol source) falls back to natural alignment rules
    /// derived from the size. This class does not invent a value.
    /// </summary>
    public sealed class DwarfResolvedStructLayout
    {
        public DwarfResolvedStructLayout(
            string? name,
            bool isUnion,
            long sizeBytes,
            long alignmentBytes,
            bool isOpaque,
            IReadOnlyList<DwarfResolvedField> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }
            if (sizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeBytes), "Struct size cannot be negative.");
            }
            if (alignmentBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alignmentBytes), "Struct alignment cannot be negative.");
            }
            if (isOpaque && fields.Count > 0)
            {
                throw new ArgumentException(
                    "An opaque struct layout cannot carry fields.", nameof(fields));
            }

            Name = name;
            IsUnion = isUnion;
            SizeBytes = sizeBytes;
            AlignmentBytes = alignmentBytes;
            IsOpaque = isOpaque;
            Fields = fields;
        }

        /// <summary>
        /// C tag name (e.g. <c>sqlite3_backup</c>), or null for an
        /// anonymous composite.
        /// </summary>
        public string? Name { get; }

        public bool IsUnion { get; }

        public long SizeBytes { get; }

        public long AlignmentBytes { get; }

        public bool IsOpaque { get; }

        public IReadOnlyList<DwarfResolvedField> Fields { get; }
    }
}
