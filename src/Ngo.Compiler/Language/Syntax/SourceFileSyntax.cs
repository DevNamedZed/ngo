// -----------------------------------------------------------------------
// <copyright file="SourceFileSyntax.cs" company="Ziad">
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
    public sealed class SourceFileSyntax : SyntaxNode
    {
        public SourceFileSyntax(PackageClauseSyntax packageClause,
            IReadOnlyList<ImportDeclarationSyntax> imports,
            IReadOnlyList<SyntaxNode> members, SyntaxToken endOfFile)
        { PackageClause = packageClause; Imports = imports; Members = members; EndOfFile = endOfFile; }
        public PackageClauseSyntax PackageClause { get; }
        public IReadOnlyList<ImportDeclarationSyntax> Imports { get; }
        /// <summary>Top-level declarations: functions, methods, types, vars, consts.</summary>
        public IReadOnlyList<SyntaxNode> Members { get; }
        public SyntaxToken EndOfFile { get; }
        public override SyntaxKind Kind => SyntaxKind.SourceFile;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return PackageClause;
            foreach (var imp in Imports) yield return imp;
            foreach (var member in Members) yield return member;
            yield return EndOfFile;
        }
    }
}
