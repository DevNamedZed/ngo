// -----------------------------------------------------------------------
// <copyright file="ReceiveExpression.cs" company="Ziad">
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
    /// <summary>
    /// &lt;-ch — receives a value from a channel.
    /// </summary>
    public sealed class ReceiveExpression : Expression
    {
        public ReceiveExpression(Expression channel, TypeSymbol elementType, TextSpan span)
            : base(span)
        {
            Channel = channel;
            ElementType = elementType;
        }

        public Expression Channel { get; }

        public TypeSymbol ElementType { get; }

        public override TypeSymbol Type => ElementType;

        public bool IsCommaOk { get; set; }

        public override NodeType NodeType => NodeType.ReceiveExpression;
    }
}
