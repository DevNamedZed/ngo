// -----------------------------------------------------------------------
// <copyright file="MethodEmitContext.cs" company="Ziad">
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
    /// Per-method emit state — one created fresh for each top-level method/function/constructor
    /// body, so there is no reset (the old <c>ResetMethodState()</c> is deleted). Owns the IL
    /// writer, locals, parameters, captured symbols, labels, defer slots, and the enclosing
    /// generic parameters. Cross-scope shared state stays on EmitContext/EmitSession.
    /// (spec/F4-EMIT-CONTEXT-HIERARCHY.md, step 4)
    ///
    /// Closures/lambdas currently share their enclosing method's context (they only save/restore
    /// the IL writer); giving them their own child contexts is a later refinement and is not
    /// required to remove the reset.
    /// </summary>
    internal sealed class MethodEmitContext
    {
        public CilWriter IL { get; set; } = null!;

        public Dictionary<Symbol, LocalSlot> Locals { get; } = new();

        public Dictionary<Symbol, int> Parameters { get; } = new();

        public HashSet<Symbol> CapturedSymbols { get; } = new();

        public string[] EnclosingGenericParamNames { get; set; } = Array.Empty<string>();

        public TypeParameterSymbol[] EnclosingGenericParamSymbols { get; set; }
            = Array.Empty<TypeParameterSymbol>();

        public Type[] EnclosingGenericParamTypes { get; set; } = Array.Empty<Type>();

        public Stack<LoopLabel> LoopLabels { get; } = new();

        public LabelSlot? FallthroughLabel { get; set; }

        public Dictionary<string, LabelSlot> GotoLabels { get; } = new();

        public Dictionary<string, LoopLabel> NamedLabels { get; } = new();

        public Dictionary<Symbol, SliceElementPointer> SliceElementPointers { get; } = new();

        public LocalSlot? DeferStack { get; set; }

        public LocalSlot? DeferReturnLocal { get; set; }

        public LabelSlot? DeferExitLabel { get; set; }
    }
}
