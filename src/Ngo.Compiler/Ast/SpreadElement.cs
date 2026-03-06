// -----------------------------------------------------------------------
// <copyright file="SpreadElement.cs" company="Ziad">
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
    /// Represents a single element from a multi-return call spread into arguments.
    /// The first element (index 0) is the CallExpression itself; subsequent elements
    /// reference the same call but extract a different tuple field.
    /// </summary>
    public sealed class SpreadElement : Expression
    {
        public SpreadElement(CallExpression source, int index, TypeSymbol type, TextSpan span)
            : base(span)
        {
            Source = source;
            Index = index;
            ElementType = type;
        }

        public CallExpression Source { get; }
        public int Index { get; }
        public TypeSymbol ElementType { get; }

        public override TypeSymbol Type => ElementType;
        public override NodeType NodeType => NodeType.SpreadElement;
    }
}
