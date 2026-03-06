// -----------------------------------------------------------------------
// <copyright file="FunctionDeclarationSyntax.cs" company="Ziad">
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
    public sealed class FunctionDeclarationSyntax : SyntaxNode
    {
        public FunctionDeclarationSyntax(SyntaxToken funcKeyword, SyntaxToken name,
            ParameterListSyntax parameters, SyntaxNode? result, BlockSyntax? body)
            : this(funcKeyword, name, null, parameters, result, body)
        { }

        public FunctionDeclarationSyntax(SyntaxToken funcKeyword, SyntaxToken name,
            TypeParameterListSyntax? typeParameters, ParameterListSyntax parameters,
            SyntaxNode? result, BlockSyntax? body)
        { FuncKeyword = funcKeyword; Name = name; TypeParameters = typeParameters; Parameters = parameters; Result = result; Body = body; }

        public SyntaxToken FuncKeyword { get; }
        public SyntaxToken Name { get; }
        public TypeParameterListSyntax? TypeParameters { get; }
        public ParameterListSyntax Parameters { get; }
        /// <summary>Return type: ParameterListSyntax (parenthesized), ExpressionSyntax (single type), or null.</summary>
        public SyntaxNode? Result { get; }
        public BlockSyntax? Body { get; }
        public override SyntaxKind Kind => SyntaxKind.FunctionDeclaration;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return FuncKeyword; yield return Name;
            if (TypeParameters != null) yield return TypeParameters;
            yield return Parameters;
            if (Result != null) yield return Result;
            if (Body != null) yield return Body;
        }
    }
}
