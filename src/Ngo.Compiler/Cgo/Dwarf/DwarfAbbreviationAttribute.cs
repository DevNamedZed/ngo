// -----------------------------------------------------------------------
// <copyright file="DwarfAbbreviationAttribute.cs" company="Ziad">
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
    /// One <c>(attribute, form)</c> pair inside a
    /// <see cref="DwarfAbbreviation"/>. DWARF 5 adds
    /// <see cref="DwarfForm.ImplicitConst"/>, which inlines a
    /// constant value into the abbreviation itself rather than
    /// storing it per-DIE; <see cref="ImplicitConstValue"/> carries
    /// that value and is meaningful only when
    /// <see cref="Form"/> is <see cref="DwarfForm.ImplicitConst"/>.
    /// </summary>
    public sealed class DwarfAbbreviationAttribute
    {
        public DwarfAbbreviationAttribute(
            DwarfAttribute attribute, DwarfForm form, long implicitConstValue)
        {
            Attribute = attribute;
            Form = form;
            ImplicitConstValue = implicitConstValue;
        }

        public DwarfAttribute Attribute { get; }

        public DwarfForm Form { get; }

        /// <summary>
        /// Inline value for <see cref="DwarfForm.ImplicitConst"/>
        /// attributes. Zero for every other form — callers must check
        /// <see cref="Form"/> before consuming this field.
        /// </summary>
        public long ImplicitConstValue { get; }
    }
}
