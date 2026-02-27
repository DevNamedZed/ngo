// -----------------------------------------------------------------------
// <copyright file="MapTypeSyntax.cs" company="Ziad">
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
    public sealed class MapTypeSyntax : ExpressionSyntax
    {
        public MapTypeSyntax(SyntaxToken mapKeyword, SyntaxToken openBracket, ExpressionSyntax keyType,
            SyntaxToken closeBracket, ExpressionSyntax valueType)
        {
            MapKeyword = mapKeyword;
            OpenBracket = openBracket;
            KeyType = keyType;
            CloseBracket = closeBracket;
            ValueType = valueType;
        }

        public SyntaxToken MapKeyword { get; }
        public SyntaxToken OpenBracket { get; }
        public ExpressionSyntax KeyType { get; }
        public SyntaxToken CloseBracket { get; }
        public ExpressionSyntax ValueType { get; }

        public override SyntaxKind Kind => SyntaxKind.MapType;

        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return MapKeyword;
            yield return OpenBracket;
            yield return KeyType;
            yield return CloseBracket;
            yield return ValueType;
        }
    }
}
