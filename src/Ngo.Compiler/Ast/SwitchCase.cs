// -----------------------------------------------------------------------
// <copyright file="SwitchCase.cs" company="Ziad">
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

namespace Ngo.Compiler.Ast
{
    public sealed class SwitchCase : AstNode
    {
        public SwitchCase(IReadOnlyList<Expression>? expressions, IReadOnlyList<AstNode> body, bool isDefault, TextSpan span)
            : base(span)
        {
            Expressions = expressions;
            Body = body;
            IsDefault = isDefault;
        }

        public IReadOnlyList<Expression>? Expressions { get; }
        public IReadOnlyList<AstNode> Body { get; }
        public bool IsDefault { get; }

        public override NodeType NodeType => NodeType.SwitchCase;
        public override NodeKind NodeKind => NodeKind.Statement;
    }
}
