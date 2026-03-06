// -----------------------------------------------------------------------
// <copyright file="SelectCase.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Ast
{
    public enum SelectCaseKind
    {
        Send,
        Receive,
        Default,
    }

    public sealed class SelectCase : AstNode
    {
        public SelectCase(SelectCaseKind kind, Expression? channel,
            Expression? sendValue, LocalSymbol? valueLocal, LocalSymbol? okLocal,
            IReadOnlyList<AstNode> body, TextSpan span)
            : base(span)
        {
            Kind = kind;
            Channel = channel;
            SendValue = sendValue;
            ValueLocal = valueLocal;
            OkLocal = okLocal;
            Body = body;
        }

        public SelectCaseKind Kind { get; }

        public Expression? Channel { get; }

        public Expression? SendValue { get; }

        public LocalSymbol? ValueLocal { get; }

        public LocalSymbol? OkLocal { get; }

        public IReadOnlyList<AstNode> Body { get; }

        public bool IsDefault => Kind == SelectCaseKind.Default;

        public override NodeType NodeType => NodeType.SelectCase;

        public override NodeKind NodeKind => NodeKind.Statement;
    }
}
