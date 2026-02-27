// -----------------------------------------------------------------------
// <copyright file="PackageClauseSyntax.cs" company="Ziad">
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
    public sealed class PackageClauseSyntax : SyntaxNode
    {
        public PackageClauseSyntax(SyntaxToken packageKeyword, SyntaxToken name)
        { PackageKeyword = packageKeyword; Name = name; }
        public SyntaxToken PackageKeyword { get; }
        public SyntaxToken Name { get; }
        public override SyntaxKind Kind => SyntaxKind.PackageClause;
        public override IEnumerable<SyntaxNode> ChildNodes()
        { yield return PackageKeyword; yield return Name; }
    }
}
