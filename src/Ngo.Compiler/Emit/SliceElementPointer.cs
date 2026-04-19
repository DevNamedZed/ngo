// -----------------------------------------------------------------------
// <copyright file="SliceElementPointer.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Tracks a variable that was assigned &amp;slice[i]. Instead of creating a
    /// Ptr&lt;T&gt; wrapping a copy, field access through this variable goes
    /// through Slice&lt;T&gt;.get_Item(index) which returns a managed reference
    /// into the backing array. This preserves Go's pointer-to-slice-element
    /// semantics where mutations through the pointer modify the slice element.
    /// </summary>
    internal sealed class SliceElementPointer
    {
        public SliceElementPointer(LocalSlot sliceLocal, LocalSlot indexLocal,
            Type sliceClrType, Type elementClrType)
        {
            SliceLocal = sliceLocal;
            IndexLocal = indexLocal;
            SliceClrType = sliceClrType;
            ElementClrType = elementClrType;
        }

        public LocalSlot SliceLocal { get; }

        public LocalSlot IndexLocal { get; }

        public Type SliceClrType { get; }

        public Type ElementClrType { get; }
    }
}
