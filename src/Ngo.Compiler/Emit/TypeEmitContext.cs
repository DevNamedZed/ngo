// -----------------------------------------------------------------------
// <copyright file="TypeEmitContext.cs" company="Ziad">
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
using System.Collections.Generic;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Per-type emit state — the type being defined and its generic parameter bindings.
    /// (spec/F4-EMIT-CONTEXT-HIERARCHY.md, step 3.) This level is thin in the current code: the
    /// active emit path carries type+method generics together as the enclosing generics on
    /// <see cref="MethodEmitContext"/>, so this is not yet wired into EmitContext. It is kept as
    /// the clean per-type owner that the emitters will receive when they are switched to take
    /// contexts directly (step 5).
    /// </summary>
    internal sealed class TypeEmitContext
    {
        public TypeEmitContext(ITypeBuilder typeBuilder)
        {
            TypeBuilder = typeBuilder;
        }

        public ITypeBuilder TypeBuilder { get; }

        public Dictionary<TypeParameterSymbol, Type> GenericParameters { get; } = new();
    }
}
