// -----------------------------------------------------------------------
// <copyright file="CallExpression.cs" company="Ziad">
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

using System.Collections.Generic;
using Ngo.Compiler.Language;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Ast
{
    public sealed class CallExpression : Expression
    {
        public CallExpression(FunctionSymbol function, IReadOnlyList<Expression> arguments, TextSpan span)
            : base(span)
        {
            Function = function;
            Arguments = arguments;
        }

        public CallExpression(FunctionSymbol function, IReadOnlyList<Expression> arguments,
            Expression callTarget, TextSpan span)
            : base(span)
        {
            Function = function;
            Arguments = arguments;
            CallTarget = callTarget;
        }

        public FunctionSymbol Function { get; }

        public IReadOnlyList<Expression> Arguments { get; }

        /// <summary>
        /// For indirect calls (function variables), the expression that evaluates to the callable delegate.
        /// Null for direct function calls.
        /// </summary>
        public Expression? CallTarget { get; }

        public IReadOnlyList<TypeSymbol>? TypeArguments { get; init; }

        public TypeSymbol? SubstitutedReturnType { get; init; }

        public IReadOnlyList<TypeSymbol>? SubstitutedReturnTypes { get; init; }

        public IReadOnlyList<TypeSymbol> EffectiveReturnTypes =>
            SubstitutedReturnTypes ?? Function.ReturnTypes;

        public bool IsSpreadArg { get; set; }

        public override TypeSymbol Type => SubstitutedReturnType ?? Function.ReturnType;

        public override NodeType NodeType => NodeType.CallExpression;
    }
}
