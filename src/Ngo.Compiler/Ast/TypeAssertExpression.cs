// -----------------------------------------------------------------------
// <copyright file="TypeAssertExpression.cs" company="Ziad">
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

using Ngo.Compiler.Language;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Ast
{
    public sealed class TypeAssertExpression : Expression
    {
        public TypeAssertExpression(Expression expression, TypeSymbol assertedType, TextSpan span)
            : base(span)
        {
            Expression = expression;
            AssertedType = assertedType;
        }

        public Expression Expression { get; }

        public TypeSymbol AssertedType { get; }

        public override TypeSymbol Type => AssertedType;

        public bool IsCommaOk { get; set; }

        public override NodeType NodeType => NodeType.TypeAssertExpression;
    }
}
