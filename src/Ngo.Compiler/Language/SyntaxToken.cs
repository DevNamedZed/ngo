// -----------------------------------------------------------------------
// <copyright file="SyntaxToken.cs" company="Ziad">
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

using System;
using System.Collections.Generic;

namespace Ngo.Compiler.Language
{
    public sealed class SyntaxToken : SyntaxNode
    {
        public static readonly IReadOnlyList<SyntaxExtra> EmptyExtra = Array.Empty<SyntaxExtra>();

        public SyntaxToken(
            SyntaxKind kind,
            string text,
            int position,
            object? value = null,
            IReadOnlyList<SyntaxExtra>? leadingExtra = null,
            IReadOnlyList<SyntaxExtra>? trailingExtra = null)
        {
            _kind = kind;
            Text = text;
            Position = position;
            Value = value;
            LeadingExtra = leadingExtra ?? EmptyExtra;
            TrailingExtra = trailingExtra ?? EmptyExtra;
        }

        private readonly SyntaxKind _kind;

        public override SyntaxKind Kind => _kind;

        public string Text { get; }

        public int Position { get; }

        public object? Value { get; }

        public IReadOnlyList<SyntaxExtra> LeadingExtra { get; }

        public IReadOnlyList<SyntaxExtra> TrailingExtra { get; }

        public override TextSpan Span => new TextSpan(Position, Text.Length);

        public TextSpan FullSpan
        {
            get
            {
                int start = LeadingExtra.Count > 0 ? LeadingExtra[0].Position : Position;
                int end = Position + Text.Length;
                if (TrailingExtra.Count > 0)
                {
                    var last = TrailingExtra[TrailingExtra.Count - 1];
                    end = last.Position + last.Text.Length;
                }
                return TextSpan.FromBounds(start, end);
            }
        }

        public bool IsMissing => Text.Length == 0;

        public override IEnumerable<SyntaxNode> ChildNodes() => Array.Empty<SyntaxNode>();

        public override string ToString() => $"{Kind} \"{Text}\"";
    }
}
