// -----------------------------------------------------------------------
// <copyright file="SourceFile.cs" company="Ziad">
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
    public sealed class SourceFile : AstNode
    {
        public SourceFile(
            PackageDeclaration package,
            IReadOnlyList<ImportDeclaration> imports,
            IReadOnlyList<FunctionDeclaration> functions,
            IReadOnlyList<MethodDeclaration> methods,
            IReadOnlyList<VarDeclaration> variables,
            IReadOnlyList<TypeDeclaration> types,
            IReadOnlyList<ConstDeclaration> constants,
            TextSpan span)
            : base(span)
        {
            Package = package;
            Imports = imports;
            Functions = functions;
            Methods = methods;
            Variables = variables;
            Types = types;
            Constants = constants;
        }

        public PackageDeclaration Package { get; }

        public IReadOnlyList<ImportDeclaration> Imports { get; }

        public IReadOnlyList<FunctionDeclaration> Functions { get; }

        public IReadOnlyList<MethodDeclaration> Methods { get; }

        public IReadOnlyList<VarDeclaration> Variables { get; }

        public IReadOnlyList<TypeDeclaration> Types { get; }

        public IReadOnlyList<ConstDeclaration> Constants { get; }

        public override NodeType NodeType => NodeType.SourceFile;

        public override NodeKind NodeKind => NodeKind.Declaration;
    }
}
