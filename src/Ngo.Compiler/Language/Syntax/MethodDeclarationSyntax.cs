// -----------------------------------------------------------------------
// <copyright file="MethodDeclarationSyntax.cs" company="Ziad">
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
    public sealed class MethodDeclarationSyntax : SyntaxNode
    {
        public MethodDeclarationSyntax(SyntaxToken funcKeyword, ParameterListSyntax receiver,
            SyntaxToken name, ParameterListSyntax parameters, SyntaxNode? result, BlockSyntax? body)
        { FuncKeyword = funcKeyword; Receiver = receiver; Name = name;
          Parameters = parameters; Result = result; Body = body; }
        public SyntaxToken FuncKeyword { get; }
        public ParameterListSyntax Receiver { get; }
        public SyntaxToken Name { get; }
        public ParameterListSyntax Parameters { get; }
        public SyntaxNode? Result { get; }
        public BlockSyntax? Body { get; }
        public override SyntaxKind Kind => SyntaxKind.MethodDeclaration;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return FuncKeyword; yield return Receiver; yield return Name; yield return Parameters;
            if (Result != null) yield return Result;
            if (Body != null) yield return Body;
        }
    }
}
