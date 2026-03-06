// -----------------------------------------------------------------------
// <copyright file="FieldDeclarationSyntax.cs" company="Ziad">
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
    public sealed class FieldDeclarationSyntax : SyntaxNode
    {
        public FieldDeclarationSyntax(SeparatedSyntaxList<SyntaxToken>? names,
            ExpressionSyntax type, SyntaxToken? tag)
        {
            Names = names;
            Type = type;
            Tag = tag;
        }

        /// <summary>Field names (null for embedded fields).</summary>
        public SeparatedSyntaxList<SyntaxToken>? Names { get; }

        public ExpressionSyntax Type { get; }

        /// <summary>Optional struct tag (string literal).</summary>
        public SyntaxToken? Tag { get; }

        public override SyntaxKind Kind => SyntaxKind.FieldDeclaration;

        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            if (Names.HasValue)
                foreach (var node in Names.Value.GetWithSeparators())
                    yield return node;
            yield return Type;
            if (Tag != null)
                yield return Tag;
        }
    }
}
