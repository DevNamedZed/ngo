// -----------------------------------------------------------------------
// <copyright file="ConstantSymbol.cs" company="Ziad">
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
    public sealed class ConstantSymbol : Symbol
    {
        public ConstantSymbol(string name, TypeSymbol type, object? value)
            : base(name, SymbolKind.Constant)
        {
            Type = type;
            Value = value;
        }

        public TypeSymbol Type { get; }

        public object? Value { get; }
    }
}
