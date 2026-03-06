// -----------------------------------------------------------------------
// <copyright file="UnionTypeSyntax.cs" company="Ziad">
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
    public sealed class UnionTypeSyntax : ExpressionSyntax
    {
        public UnionTypeSyntax(IReadOnlyList<UnionTermSyntax> terms)
        { Terms = terms; }

        public IReadOnlyList<UnionTermSyntax> Terms { get; }

        public override SyntaxKind Kind => SyntaxKind.UnionType;

        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            foreach (var term in Terms)
                yield return term;
        }
    }

    public sealed class UnionTermSyntax : SyntaxNode
    {
        public UnionTermSyntax(SyntaxToken? tilde, ExpressionSyntax type, SyntaxToken? pipe)
        { Tilde = tilde; Type = type; Pipe = pipe; }

        public SyntaxToken? Tilde { get; }
        public ExpressionSyntax Type { get; }
        public SyntaxToken? Pipe { get; }

        public override SyntaxKind Kind => SyntaxKind.UnionTerm;

        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            if (Tilde != null) yield return Tilde;
            yield return Type;
            if (Pipe != null) yield return Pipe;
        }
    }
}
