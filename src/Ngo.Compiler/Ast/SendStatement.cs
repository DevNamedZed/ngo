// -----------------------------------------------------------------------
// <copyright file="SendStatement.cs" company="Ziad">
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
    /// <summary>
    /// ch &lt;- value — sends a value on a channel.
    /// </summary>
    public sealed class SendStatement : Statement
    {
        public SendStatement(Expression channel, Expression value, TextSpan span)
            : base(span)
        {
            Channel = channel;
            Value = value;
        }

        public Expression Channel { get; }

        public Expression Value { get; }

        public override NodeType NodeType => NodeType.SendStatement;
    }
}
