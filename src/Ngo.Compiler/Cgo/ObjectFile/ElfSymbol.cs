// -----------------------------------------------------------------------
// <copyright file="ElfSymbol.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// A single parsed entry from an ELF64 symbol table (SHT_SYMTAB).
    /// The reader emits one instance per <c>Elf64_Sym</c> row so the
    /// relocation applier can resolve each <see cref="ElfRelocation.SymbolIndex"/>
    /// to a symbol value. Section symbols — <c>STT_SECTION</c> — have a
    /// <see cref="Value"/> of zero in unlinked object files and
    /// represent the base address of the section identified by
    /// <see cref="SectionHeaderIndex"/>; cgo anchor probes rely on
    /// those almost exclusively for DWARF cross-references.
    /// </summary>
    public sealed class ElfSymbol
    {
        public ElfSymbol(string name, ulong value, ushort sectionHeaderIndex)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Value = value;
            SectionHeaderIndex = sectionHeaderIndex;
        }

        /// <summary>
        /// Symbol's display name resolved through the associated string
        /// table. Empty when <c>st_name</c> is zero, which is normal
        /// for the undefined symbol at index 0 and for
        /// <c>STT_SECTION</c> symbols in some toolchains.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Symbol's address or offset value (<c>st_value</c>). Zero for
        /// section symbols in unlinked object files.
        /// </summary>
        public ulong Value { get; }

        /// <summary>
        /// Section header table index where the symbol is defined
        /// (<c>st_shndx</c>). For section symbols this is the target
        /// section the symbol stands for.
        /// </summary>
        public ushort SectionHeaderIndex { get; }
    }
}
