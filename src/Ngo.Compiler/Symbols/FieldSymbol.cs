// -----------------------------------------------------------------------
// <copyright file="FieldSymbol.cs" company="Ziad">
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

namespace Ngo.Compiler.Symbols
{
    public sealed class FieldSymbol : Symbol
    {
        public FieldSymbol(string name, TypeSymbol type, int ordinal, bool isEmbedded = false)
            : base(name, SymbolKind.Field)
        {
            Type = type;
            Ordinal = ordinal;
            IsEmbedded = isEmbedded;
        }

        public TypeSymbol Type { get; }

        public int Ordinal { get; }

        public bool IsEmbedded { get; }
    }
}
