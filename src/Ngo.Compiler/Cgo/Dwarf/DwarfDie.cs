// -----------------------------------------------------------------------
// <copyright file="DwarfDie.cs" company="Ziad">
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
    /// One Debug Information Entry. A DIE is a classification tag
    /// plus a set of attribute values plus an optional ordered list
    /// of child DIEs. The sibling chain from <c>.debug_info</c> is
    /// materialised into <see cref="Children"/>; the null DIE that
    /// terminates each chain is not stored — its only job is to
    /// mark the end of the current level of the walk.
    ///
    /// <see cref="OffsetInDebugInfo"/> is the absolute byte offset
    /// of this DIE's first byte inside <c>.debug_info</c>, so
    /// cross-references (<see cref="DwarfReferenceAttributeValue"/>)
    /// can address it through
    /// <see cref="DwarfCompilationUnit.DiesByOffsetInDebugInfo"/>.
    /// </summary>
    public sealed class DwarfDie
    {
        public DwarfDie(
            DwarfTag tag,
            int offsetInDebugInfo,
            IReadOnlyDictionary<DwarfAttribute, DwarfAttributeValue> attributes,
            IReadOnlyList<DwarfDie> children)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            Tag = tag;
            OffsetInDebugInfo = offsetInDebugInfo;
            Attributes = attributes;
            Children = children;
        }

        public DwarfTag Tag { get; }

        public int OffsetInDebugInfo { get; }

        public IReadOnlyDictionary<DwarfAttribute, DwarfAttributeValue> Attributes { get; }

        public IReadOnlyList<DwarfDie> Children { get; }

        /// <summary>
        /// Return the value for <paramref name="attribute"/> if
        /// present, otherwise null. Chosen over dictionary-indexer
        /// throw behaviour so consumers can express "optional
        /// attribute" without wrapping every lookup in a containment
        /// check.
        /// </summary>
        public DwarfAttributeValue? TryGetAttribute(DwarfAttribute attribute)
        {
            return Attributes.TryGetValue(attribute, out DwarfAttributeValue? value) ? value : null;
        }
    }
}
