// -----------------------------------------------------------------------
// <copyright file="IfStatement.cs" company="Ziad">
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

namespace Ngo.Compiler.Ast
{
    public sealed class IfStatement : Statement
    {
        public IfStatement(AstNode? init, Expression condition, BlockStatement body, AstNode? elseBody, TextSpan span)
            : base(span)
        {
            Init = init;
            Condition = condition;
            Body = body;
            ElseBody = elseBody;
        }

        public AstNode? Init { get; }
        public Expression Condition { get; }
        public BlockStatement Body { get; }
        public AstNode? ElseBody { get; }

        public override NodeType NodeType => NodeType.IfStatement;
    }
}
