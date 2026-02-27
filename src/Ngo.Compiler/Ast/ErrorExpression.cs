// -----------------------------------------------------------------------
// <copyright file="ErrorExpression.cs" company="Ziad">
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
    public sealed class ErrorExpression : Expression
    {
        public ErrorExpression(string message, TextSpan span)
            : base(span)
        {
            Message = message;
        }

        public string Message { get; }

        public override TypeSymbol Type => TypeSymbol.Error;

        public override NodeType NodeType => NodeType.ErrorExpression;
    }
}
