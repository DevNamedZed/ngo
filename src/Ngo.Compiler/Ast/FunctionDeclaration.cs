// -----------------------------------------------------------------------
// <copyright file="FunctionDeclaration.cs" company="Ziad">
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
using Ngo.Compiler.Language;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Ast
{
    public sealed class FunctionDeclaration : AstNode
    {
        public FunctionDeclaration(FunctionSymbol symbol, BlockStatement body, TextSpan span)
            : this(symbol, body, Array.Empty<LocalSymbol>(), span)
        {
        }

        public FunctionDeclaration(FunctionSymbol symbol, BlockStatement body,
            IReadOnlyList<LocalSymbol> namedReturns, TextSpan span)
            : base(span)
        {
            Symbol = symbol;
            Body = body;
            NamedReturns = namedReturns;
        }

        public FunctionSymbol Symbol { get; }

        public BlockStatement Body { get; }

        public IReadOnlyList<LocalSymbol> NamedReturns { get; }

        public override NodeType NodeType => NodeType.FunctionDeclaration;

        public override NodeKind NodeKind => NodeKind.Declaration;
    }
}
