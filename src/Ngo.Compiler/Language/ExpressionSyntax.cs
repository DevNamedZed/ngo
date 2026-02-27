// -----------------------------------------------------------------------
// <copyright file="ExpressionSyntax.cs" company="Ziad">
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

namespace Ngo.Compiler.Language
{
    /// <summary>
    /// Base class for all expression nodes. Type syntax nodes also extend this,
    /// since Go types can appear in expression positions (composite literals,
    /// type assertions, conversions).
    /// </summary>
    public abstract class ExpressionSyntax : SyntaxNode
    {
    }
}
