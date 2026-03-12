// -----------------------------------------------------------------------
// <copyright file="LocalBinding.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Associates a local variable symbol with its declaration location.
    /// Used by AnalysisContext to track function-scoped locals for unused-variable checks.
    /// </summary>
    public sealed class LocalBinding
    {
        public LocalBinding(LocalSymbol symbol, TextSpan span)
        {
            Symbol = symbol;
            Span = span;
        }

        public LocalSymbol Symbol { get; }

        public TextSpan Span { get; }
    }
}
