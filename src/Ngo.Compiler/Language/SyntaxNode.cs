// -----------------------------------------------------------------------
// <copyright file="SyntaxNode.cs" company="Ziad">
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

namespace Ngo.Compiler.Language
{
    public abstract class SyntaxNode
    {
        public abstract SyntaxKind Kind { get; }

        public SyntaxNode? Parent { get; internal set; }

        public virtual TextSpan Span
        {
            get
            {
                SyntaxNode? first = null, last = null;
                foreach (var child in ChildNodes())
                {
                    first ??= child;
                    last = child;
                }

                if (first == null) return default;
                return TextSpan.FromBounds(first.Span.Start, last!.Span.End);
            }
        }

        public abstract IEnumerable<SyntaxNode> ChildNodes();

        public int SpanStart => Span.Start;

        public IEnumerable<SyntaxToken> DescendantTokens()
        {
            foreach (var child in ChildNodes())
            {
                if (child is SyntaxToken token)
                    yield return token;
                else
                    foreach (var t in child.DescendantTokens())
                        yield return t;
            }
        }

        public override string ToString() => Kind.ToString();
    }
}
