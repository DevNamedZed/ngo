// -----------------------------------------------------------------------
// <copyright file="EmitContext.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Shared mutable context passed to all emitter components.
    /// </summary>
    internal sealed class EmitContext
    {
        public EmitContext(ModuleBuilder module, TypeMapper mapper, EmitOptions? options = null)
        {
            Module = module;
            Mapper = mapper;
            Options = options ?? EmitOptions.Default;
        }

        public ModuleBuilder Module { get; }
        public ModuleBuilder ModuleBuilder => Module;
        public TypeMapper Mapper { get; }
        public EmitOptions Options { get; }
        public TypeBuilder PackageType { get; set; } = null!;

        // Track types that have been finalized (CreateType called) across packages
        public HashSet<TypeSymbol> FinalizedTypes { get; } = new();
        public DeclarationEmitter? DeclEmitter { get; set; }

        // Per-method state (reset for each method body)
        public ILGenerator IL { get; set; } = null!;
        public Dictionary<Symbol, LocalBuilder> Locals { get; } = new();
        public Dictionary<Symbol, int> Parameters { get; } = new();

        // Symbols captured by closures in the current function body (stored in Box<T>)
        public HashSet<Symbol> CapturedSymbols { get; } = new();

        // All emitted methods (for resolving calls)
        public Dictionary<Symbol, MethodBuilder> Methods { get; } = new();

        // Loop label stack for break/continue
        public Stack<(Label breakLabel, Label continueLabel)> LoopLabels { get; } = new();

        // Fallthrough target label for switch cases
        public Label? FallthroughLabel { get; set; }

        // Goto target labels: "labelName" → IL label
        public Dictionary<string, Label> GotoLabels { get; } = new();

        // Named labels for labeled break/continue: "labelName" → (breakLabel, continueLabel)
        public Dictionary<string, (Label breakLabel, Label continueLabel)> NamedLabels { get; } = new();

        // Package-level fields (var declarations)
        public Dictionary<Symbol, FieldBuilder> PackageFields { get; } = new();

        // Struct type builders (for composite literals and field access)
        public Dictionary<TypeSymbol, TypeBuilder> StructTypes { get; } = new();

        // Struct field builders (FieldSymbol → FieldBuilder)
        public Dictionary<FieldSymbol, FieldBuilder> StructFields { get; } = new();

        // Interface type builders (InterfaceTypeSymbol → TypeBuilder)
        public Dictionary<InterfaceTypeSymbol, TypeBuilder> InterfaceTypes { get; } = new();

        // Wrapper types for interface satisfaction: (concrete, interface) → (wrapperType, ctor)
        public Dictionary<(TypeSymbol, InterfaceTypeSymbol), (Type type, ConstructorInfo ctor)> WrapperTypes { get; } = new();

        // Defer stack local for the current method (null if no defer statements)
        public LocalBuilder? DeferStack { get; set; }

        // For non-void defer-wrapped functions: store return value here, then leave
        public LocalBuilder? DeferReturnLocal { get; set; }
        public Label DeferExitLabel { get; set; }

        public string QualifyName(string name) =>
            Options.Namespace != null ? $"{Options.Namespace}.{name}" : name;

        public bool IsExported(string goName) =>
            goName.Length > 0 && char.IsUpper(goName[0]);

        public void ResetMethodState()
        {
            Locals.Clear();
            Parameters.Clear();
            NamedLabels.Clear();
            CapturedSymbols.Clear();
            DeferStack = null;
            DeferReturnLocal = null;
        }
    }
}
