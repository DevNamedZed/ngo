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

using System;
using System.Collections.Generic;

namespace Ngo.Compiler.Symbols
{
    public sealed class StructTypeSymbol : TypeSymbol
    {
        public StructTypeSymbol(string name, IReadOnlyList<FieldSymbol> fields, string? packagePath = null)
            : base(name, TypeKind.Struct, null, packagePath)
        {
            Fields = fields;
        }

        public StructTypeSymbol(string name, IReadOnlyList<FieldSymbol> fields, StructTypeSymbol underlying, string? packagePath = null)
            : base(name, TypeKind.Struct, underlying, packagePath)
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
        /// Returns null if not found.
        /// </summary>
        public PromotedMethodResult? LookupPromotedMethod(string name)
        {
            return LookupPromotedMethod(name, new HashSet<StructTypeSymbol>());
        }

        private PromotedMethodResult? LookupPromotedMethod(string name, HashSet<StructTypeSymbol> visited)
        {
            if (!visited.Add(this))
            {
                return null;
            }

            for (int i = 0; i < Fields.Count; i++)
            {
                if (!Fields[i].IsEmbedded)
                {
                    continue;
                }
                // Look through pointer types for embedded methods
                var embeddedType = Fields[i].Type is PointerTypeSymbol ptr
                    ? ptr.ElementType
                    : Fields[i].Type;
                // Unwrap type aliases (e.g., type Option = option.Interface)
                var lookupType = embeddedType;
                while (lookupType.IsAlias && lookupType.UnderlyingType != null)
                {
                    lookupType = lookupType.UnderlyingType;
                }
                var method = lookupType.LookupMethod(name);
                if (method != null)
                {
                    return new PromotedMethodResult(Fields[i], method);
                }

                if (method == null && lookupType.UnderlyingType != null)
                {
                    method = lookupType.UnderlyingType.LookupMethod(name);
                    if (method != null)
                    {
                        return new PromotedMethodResult(Fields[i], method);
                    }
                }

                var resolvedEmbedded = lookupType is InstantiatedTypeSymbol inst2
                    ? inst2.Resolved() : lookupType;
                if (resolvedEmbedded is not StructTypeSymbol && resolvedEmbedded.Resolved() is StructTypeSymbol)
                {
                    resolvedEmbedded = resolvedEmbedded.Resolved();
                }
                if (resolvedEmbedded is StructTypeSymbol embeddedStruct)
                {
                    var deep = embeddedStruct.LookupPromotedMethod(name, visited);
                    if (deep != null)
                    {
                        return new PromotedMethodResult(Fields[i], deep.Method);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Looks up a promoted field from embedded structs.
        /// Returns null if not found.
        /// </summary>
        public PromotedFieldResult? LookupPromotedField(string name)
        {
            return LookupPromotedField(name, new HashSet<StructTypeSymbol>());
        }

        private PromotedFieldResult? LookupPromotedField(string name, HashSet<StructTypeSymbol> visited)
        {
            if (!visited.Add(this))
            {
                return null;
            }

            for (int i = 0; i < Fields.Count; i++)
            {
                if (!Fields[i].IsEmbedded)
                {
                    continue;
                }
                var embeddedType = Fields[i].Type;
                // Unwrap pointer for *T embedded fields
                if (embeddedType is PointerTypeSymbol ptr)
                {
                    embeddedType = ptr.ElementType;
                }
                // Unwrap generic instantiation (e.g., node[N, T] → node)
                if (embeddedType is InstantiatedTypeSymbol inst)
                {
                    embeddedType = inst.Resolved();
                }
                // Unwrap named type definitions (e.g., type authDecV10 authDec → authDec)
                if (embeddedType is not StructTypeSymbol)
                {
                    var resolved = embeddedType.Resolved();
                    if (resolved != embeddedType && resolved is StructTypeSymbol)
                    {
                        embeddedType = resolved;
                    }
                    else if (embeddedType.UnderlyingType is StructTypeSymbol)
                    {
                        embeddedType = embeddedType.UnderlyingType;
                    }
                }
                if (embeddedType is StructTypeSymbol embeddedStruct)
                {
                    var inner = embeddedStruct.LookupField(name);
                    if (inner != null)
                    {
                        return new PromotedFieldResult(Fields[i], inner);
                    }

                    // Recurse into deeper embedded structs
                    var deep = embeddedStruct.LookupPromotedField(name, visited);
                    if (deep != null)
                    {
                        return new PromotedFieldResult(Fields[i], deep.PromotedField);
                    }
                }
            }

            return null;
        }
    }
}
