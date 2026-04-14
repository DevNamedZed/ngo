// -----------------------------------------------------------------------
// <copyright file="DwarfStringAttributeValue.cs" company="Ziad">
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
    /// String-valued DIE attribute. The stored value is the decoded
    /// UTF-8 content regardless of which DWARF form carried it —
    /// inline <see cref="DwarfForm.String"/>, <c>.debug_str</c>
    /// offset via <see cref="DwarfForm.Strp"/>, or the DWARF 5
    /// <c>.debug_line_str</c> offset via
    /// <see cref="DwarfForm.LineStrp"/>. The caller does not need to
    /// consult <see cref="DwarfAttributeValue.Form"/> to know where
    /// the bytes came from.
    /// </summary>
    public sealed class DwarfStringAttributeValue : DwarfAttributeValue
    {
        public DwarfStringAttributeValue(DwarfForm form, string value)
            : base(form)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            Value = value;
        }

        public string Value { get; }

        public override T Accept<T>(IDwarfAttributeValueVisitor<T> visitor)
        {
            return visitor.VisitString(this);
        }
    }
}
