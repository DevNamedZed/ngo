// -----------------------------------------------------------------------
// <copyright file="StructTypeSymbol.cs" company="Ziad">
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

namespace Ngo.Compiler.Symbols
{
    public sealed class StructTypeSymbol : TypeSymbol
    {
        public StructTypeSymbol(string name, IReadOnlyList<FieldSymbol> fields)
            : base(name, TypeKind.Struct, null)
        {
            Fields = fields;
        }

        public IReadOnlyList<FieldSymbol> Fields { get; private set; }

        public void SetFields(IReadOnlyList<FieldSymbol> fields)
        {
            Fields = fields;
        }

        public FieldSymbol? LookupField(string name)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                if (Fields[i].Name == name)
                    return Fields[i];
            }

            return null;
        }

        /// <summary>
        /// Looks up a promoted method from embedded structs.
        /// Returns null if not found. If found, returns the (embeddedField, method) pair.
        /// </summary>
        public (FieldSymbol embeddedField, MethodSymbol method)? LookupPromotedMethod(string name)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                if (!Fields[i].IsEmbedded) continue;
                var method = Fields[i].Type.LookupMethod(name);
                if (method != null)
                    return (Fields[i], method);
            }

            return null;
        }

        /// <summary>
        /// Looks up a promoted field from embedded structs.
        /// Returns null if not found. If found, returns the (embeddedField, promotedField) pair.
        /// </summary>
        public (FieldSymbol embeddedField, FieldSymbol promotedField)? LookupPromotedField(string name)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                if (!Fields[i].IsEmbedded) continue;
                if (Fields[i].Type is StructTypeSymbol embeddedStruct)
                {
                    var inner = embeddedStruct.LookupField(name);
                    if (inner != null)
                        return (Fields[i], inner);
                }
            }

            return null;
        }
    }
}
