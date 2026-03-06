// -----------------------------------------------------------------------
// <copyright file="CompositeLiteralExpression.cs" company="Ziad">
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
    public sealed class CompositeLiteralExpression : Expression
    {
        public CompositeLiteralExpression(TypeSymbol type, IReadOnlyList<FieldInitializer> initializers, TextSpan span)
            : base(span)
        {
            Type = type;
            Initializers = initializers;
        }

        public CompositeLiteralExpression(TypeSymbol type, IReadOnlyList<ElementInitializer> elements, TextSpan span)
            : base(span)
        {
            Type = type;
            Elements = elements;
        }

        public override TypeSymbol Type { get; }

        public IReadOnlyList<FieldInitializer>? Initializers { get; }

        public IReadOnlyList<ElementInitializer>? Elements { get; }

        public override NodeType NodeType => NodeType.CompositeLiteralExpression;
    }
}
