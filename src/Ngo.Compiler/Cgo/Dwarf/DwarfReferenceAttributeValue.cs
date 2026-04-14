// -----------------------------------------------------------------------
// <copyright file="DwarfReferenceAttributeValue.cs" company="Ziad">
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
    /// Cross-reference DIE attribute. Stores the absolute byte
    /// offset of the target DIE inside <c>.debug_info</c>. The
    /// reader resolves every form's CU-relative or unit-relative
    /// encoding into an absolute offset before this value is
    /// constructed, so the Layer-4 type resolver can look the DIE
    /// up in <see cref="DwarfCompilationUnit.DiesByOffsetInDebugInfo"/>
    /// without knowing which form carried the reference.
    /// </summary>
    public sealed class DwarfReferenceAttributeValue : DwarfAttributeValue
    {
        public DwarfReferenceAttributeValue(DwarfForm form, int offsetInDebugInfo)
            : base(form)
        {
            OffsetInDebugInfo = offsetInDebugInfo;
        }

        public int OffsetInDebugInfo { get; }

        public override T Accept<T>(IDwarfAttributeValueVisitor<T> visitor)
        {
            return visitor.VisitReference(this);
        }
    }
}
