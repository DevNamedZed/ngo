// -----------------------------------------------------------------------
// <copyright file="DwarfIntegerAttributeValue.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// Integer-valued DIE attribute. Covers address, fixed-width
    /// data, LEB128 data, section-offset and implicit-const forms.
    /// The reader widens every fixed-width unsigned quantity to
    /// <see cref="long"/> and records <see cref="IsUnsigned"/> so a
    /// downstream consumer reading <c>DW_AT_byte_size</c> as an
    /// unsigned count does not get a spurious negative interpretation
    /// from a <c>data8</c> with the high bit set.
    /// </summary>
    public sealed class DwarfIntegerAttributeValue : DwarfAttributeValue
    {
        public DwarfIntegerAttributeValue(DwarfForm form, long value, bool isUnsigned)
            : base(form)
        {
            Value = value;
            IsUnsigned = isUnsigned;
        }

        public long Value { get; }

        /// <summary>
        /// True when the encoding form is intrinsically unsigned
        /// (<c>data*</c>, <c>udata</c>, <c>addr</c>, <c>sec_offset</c>,
        /// <c>flag</c>). False only for <see cref="DwarfForm.Sdata"/>
        /// and <see cref="DwarfForm.ImplicitConst"/>, which encode a
        /// signed quantity.
        /// </summary>
        public bool IsUnsigned { get; }

        public override T Accept<T>(IDwarfAttributeValueVisitor<T> visitor)
        {
            return visitor.VisitInteger(this);
        }
    }
}
