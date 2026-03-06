// -----------------------------------------------------------------------
// <copyright file="SliceExpression.cs" company="Ziad">
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
    public sealed class SliceExpression : Expression
    {
        public SliceExpression(Expression operand, Expression? low, Expression? high,
            Expression? max, TypeSymbol type, TextSpan span)
            : base(span)
        {
            Operand = operand;
            Low = low;
            High = high;
            Max = max;
            Type = type;
        }

        public Expression Operand { get; }

        public Expression? Low { get; }

        public Expression? High { get; }

        public Expression? Max { get; }

        public override TypeSymbol Type { get; }

        public override NodeType NodeType => NodeType.SliceExpression;
    }
}
