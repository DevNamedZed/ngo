// -----------------------------------------------------------------------
// <copyright file="MapTypeParts.cs" company="Ziad">
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

using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// The parsed key and value type symbols from a Go map type string.
    /// </summary>
    public sealed class MapTypeParts
    {
        public MapTypeParts(TypeSymbol? keyType, TypeSymbol? valueType)
        {
            KeyType = keyType;
            ValueType = valueType;
        }

        public TypeSymbol? KeyType { get; }

        public TypeSymbol? ValueType { get; }
    }
}
