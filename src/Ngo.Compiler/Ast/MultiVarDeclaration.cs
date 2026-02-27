// -----------------------------------------------------------------------
// <copyright file="MultiVarDeclaration.cs" company="Ziad">
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
    /// <summary>
    /// Represents a multi-value variable declaration: a, b := f()
    /// where f() returns multiple values (a tuple).
    /// Symbols may be null for blank identifiers (_).
    /// </summary>
    public sealed class MultiVarDeclaration : AstNode
    {
        public MultiVarDeclaration(IReadOnlyList<LocalSymbol?> symbols,
            Expression initializer, TextSpan span)
            : base(span)
        {
            Symbols = symbols;
            Initializer = initializer;
        }

        public IReadOnlyList<LocalSymbol?> Symbols { get; }

        public Expression Initializer { get; }

        public override NodeType NodeType => NodeType.MultiVarDeclaration;

        public override NodeKind NodeKind => NodeKind.Declaration;
    }
}
