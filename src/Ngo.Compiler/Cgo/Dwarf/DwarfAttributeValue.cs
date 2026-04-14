// -----------------------------------------------------------------------
// <copyright file="DwarfAttributeValue.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// One decoded attribute value attached to a DIE. The concrete
    /// subclass encodes what kind of payload the DWARF form carried:
    /// integer, string, block of bytes, flag, or cross-reference
    /// into <c>.debug_info</c>. Callers dispatch through
    /// <see cref="Accept{T}"/> or the typed <c>As*</c> accessors,
    /// both of which fail loudly on a form/type mismatch. This is
    /// hardening item #9 — a mismatched access must never silently
    /// return null or a defaulted value.
    /// </summary>
    public abstract class DwarfAttributeValue
    {
        protected DwarfAttributeValue(DwarfForm form)
        {
            Form = form;
        }

        /// <summary>
        /// Exact DWARF form the value was encoded with. Preserved so
        /// a downstream hint about the producer (e.g. whether an
        /// integer arrived as <c>sdata</c> or <c>data4</c>) survives
        /// the parse.
        /// </summary>
        public DwarfForm Form { get; }

        public abstract T Accept<T>(IDwarfAttributeValueVisitor<T> visitor);

        /// <summary>
        /// Extract the integer payload. Throws when the value is not
        /// a <see cref="DwarfIntegerAttributeValue"/> — hardening
        /// item #9.
        /// </summary>
        public long AsInteger()
        {
            if (this is DwarfIntegerAttributeValue integer)
            {
                return integer.Value;
            }
            throw BuildMismatchException("integer");
        }

        public string AsString()
        {
            if (this is DwarfStringAttributeValue str)
            {
                return str.Value;
            }
            throw BuildMismatchException("string");
        }

        public byte[] AsBlock()
        {
            if (this is DwarfBlockAttributeValue block)
            {
                return block.Value;
            }
            throw BuildMismatchException("block");
        }

        public bool AsFlag()
        {
            if (this is DwarfFlagAttributeValue flag)
            {
                return flag.Value;
            }
            throw BuildMismatchException("flag");
        }

        public int AsReference()
        {
            if (this is DwarfReferenceAttributeValue reference)
            {
                return reference.OffsetInDebugInfo;
            }
            throw BuildMismatchException("reference");
        }

        private InvalidOperationException BuildMismatchException(string requestedKind)
        {
            return new InvalidOperationException(
                "DWARF attribute value with form " + Form + " cannot be read as a " +
                requestedKind + "; concrete runtime type is " + GetType().Name + ".");
        }
    }
}
