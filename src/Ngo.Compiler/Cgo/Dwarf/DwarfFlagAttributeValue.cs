// -----------------------------------------------------------------------
// <copyright file="DwarfFlagAttributeValue.cs" company="Ziad">
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
    /// Boolean-flag DIE attribute. Produced from
    /// <see cref="DwarfForm.Flag"/> (one byte, truthy iff non-zero)
    /// and <see cref="DwarfForm.FlagPresent"/> (zero bytes, always
    /// true). Kept as a distinct class so a consumer never has to
    /// remember which form means "interpret as boolean" versus
    /// "interpret as small integer".
    /// </summary>
    public sealed class DwarfFlagAttributeValue : DwarfAttributeValue
    {
        public DwarfFlagAttributeValue(DwarfForm form, bool value)
            : base(form)
        {
            Value = value;
        }

        public bool Value { get; }

        public override T Accept<T>(IDwarfAttributeValueVisitor<T> visitor)
        {
            return visitor.VisitFlag(this);
        }
    }
}
