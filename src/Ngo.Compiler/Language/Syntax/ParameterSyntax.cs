// -----------------------------------------------------------------------
// <copyright file="ParameterSyntax.cs" company="Ziad">
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

namespace Ngo.Compiler.Language.Syntax
{
    public sealed class ParameterSyntax : SyntaxNode
    {
        public ParameterSyntax(SeparatedSyntaxList<SyntaxToken>? names,
            SyntaxToken? ellipsis, ExpressionSyntax? type)
        { Names = names; Ellipsis = ellipsis; Type = type; }
        /// <summary>Parameter names (null for unnamed parameters).</summary>
        public SeparatedSyntaxList<SyntaxToken>? Names { get; }
        /// <summary>Ellipsis token for variadic parameters.</summary>
        public SyntaxToken? Ellipsis { get; }
        /// <summary>Parameter type (may be null in some intermediate parse states).</summary>
        public ExpressionSyntax? Type { get; }
        public override SyntaxKind Kind => SyntaxKind.Parameter;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            if (Names.HasValue)
                foreach (var node in Names.Value.GetWithSeparators()) yield return node;
            if (Ellipsis != null) yield return Ellipsis;
            if (Type != null) yield return Type;
        }
    }
}
