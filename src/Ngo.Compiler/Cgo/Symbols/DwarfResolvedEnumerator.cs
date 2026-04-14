// -----------------------------------------------------------------------
// <copyright file="DwarfResolvedEnumerator.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// One entry inside a <see cref="DwarfResolvedEnum"/>. The value
    /// is stored as signed <see cref="long"/> — DWARF
    /// <c>DW_AT_const_value</c> allows both signed and unsigned
    /// forms, and callers reinterpret the bits per
    /// <see cref="DwarfResolvedEnum.IsSigned"/>.
    /// </summary>
    public sealed class DwarfResolvedEnumerator
    {
        public DwarfResolvedEnumerator(string name, long value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Value = value;
        }

        public string Name { get; }

        public long Value { get; }
    }
}
