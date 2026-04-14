// -----------------------------------------------------------------------
// <copyright file="DwarfBlockAttributeValue.cs" company="Ziad">
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
    /// Raw-bytes DIE attribute. Carries the payload of
    /// <c>block</c>/<c>block1</c>/<c>block2</c>/<c>block4</c> and
    /// <c>exprloc</c> forms — location expressions, opaque vendor
    /// data, and the 16-byte <see cref="DwarfForm.Data16"/>
    /// constant. The Layer-4 location-expression evaluator parses
    /// the bytes when it needs them; the reader here only preserves
    /// them exactly so the downstream parse has the whole truth to
    /// work from.
    /// </summary>
    public sealed class DwarfBlockAttributeValue : DwarfAttributeValue
    {
        public DwarfBlockAttributeValue(DwarfForm form, byte[] value)
            : base(form)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            Value = value;
        }

        public byte[] Value { get; }

        public override T Accept<T>(IDwarfAttributeValueVisitor<T> visitor)
        {
            return visitor.VisitBlock(this);
        }
    }
}
