// -----------------------------------------------------------------------
// <copyright file="SendStatementSyntax.cs" company="Ziad">
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
    public sealed class SendStatementSyntax : SyntaxNode
    {
        public SendStatementSyntax(ExpressionSyntax channel, SyntaxToken arrow, ExpressionSyntax value)
        { Channel = channel; Arrow = arrow; Value = value; }
        public ExpressionSyntax Channel { get; }
        /// <summary>The &lt;- token.</summary>
        public SyntaxToken Arrow { get; }
        public ExpressionSyntax Value { get; }
        public override SyntaxKind Kind => SyntaxKind.SendStatement;
        public override IEnumerable<SyntaxNode> ChildNodes()
        { yield return Channel; yield return Arrow; yield return Value; }
    }
}
