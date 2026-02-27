// -----------------------------------------------------------------------
// <copyright file="ChannelTypeSyntax.cs" company="Ziad">
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
    public sealed class ChannelTypeSyntax : ExpressionSyntax
    {
        public ChannelTypeSyntax(SyntaxToken? receiveArrow, SyntaxToken chanKeyword,
            SyntaxToken? sendArrow, ExpressionSyntax elementType)
        {
            ReceiveArrow = receiveArrow;
            ChanKeyword = chanKeyword;
            SendArrow = sendArrow;
            ElementType = elementType;
        }

        /// <summary>Arrow token before chan keyword, for &lt;-chan (receive-only).</summary>
        public SyntaxToken? ReceiveArrow { get; }

        public SyntaxToken ChanKeyword { get; }

        /// <summary>Arrow token after chan keyword, for chan&lt;- (send-only).</summary>
        public SyntaxToken? SendArrow { get; }

        public ExpressionSyntax ElementType { get; }

        public override SyntaxKind Kind => SyntaxKind.ChannelType;

        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            if (ReceiveArrow != null)
                yield return ReceiveArrow;
            yield return ChanKeyword;
            if (SendArrow != null)
                yield return SendArrow;
            yield return ElementType;
        }
    }
}
