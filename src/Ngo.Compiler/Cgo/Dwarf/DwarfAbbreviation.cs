// -----------------------------------------------------------------------
// <copyright file="DwarfAbbreviation.cs" company="Ziad">
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
    /// One entry in a DWARF abbreviation table — the schema for DIEs
    /// that will reference it by <see cref="Code"/> inside
    /// <c>.debug_info</c>. Carries the DIE's tag, whether child DIEs
    /// follow in the sibling chain, and the ordered list of
    /// attribute specs the DIE's attribute values are laid out
    /// against. The list order matches the order attribute values
    /// appear in <c>.debug_info</c>; the DIE parser decodes them in
    /// lockstep.
    /// </summary>
    public sealed class DwarfAbbreviation
    {
        public DwarfAbbreviation(
            int code,
            DwarfTag tag,
            bool hasChildren,
            IReadOnlyList<DwarfAbbreviationAttribute> attributes)
        {
            if (code <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(code),
                    "Abbreviation code must be positive; got " + code +
                    ". The zero code is reserved as the table terminator.");
            }
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            Code = code;
            Tag = tag;
            HasChildren = hasChildren;
            Attributes = attributes;
        }

        /// <summary>
        /// Positive numeric code compilers assign to distinguish
        /// abbreviations within a single abbrev table. Zero is
        /// reserved as the terminator and never appears on a
        /// parsed abbreviation.
        /// </summary>
        public int Code { get; }

        public DwarfTag Tag { get; }

        /// <summary>
        /// True when DIEs described by this abbreviation are followed
        /// by a sibling chain of child DIEs. The child chain ends at
        /// a DIE whose abbreviation code is zero — that null DIE is
        /// synthesised by the parser, not stored here.
        /// </summary>
        public bool HasChildren { get; }

        public IReadOnlyList<DwarfAbbreviationAttribute> Attributes { get; }
    }
}
