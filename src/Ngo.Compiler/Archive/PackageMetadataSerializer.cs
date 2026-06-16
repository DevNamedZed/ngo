// -----------------------------------------------------------------------
// <copyright file="PackageMetadataSerializer.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// Converts between TypeSymbol and Go-like type strings for binary serialization.
    /// Used by NgoArchive for the .ngo binary format.
    /// </summary>
    public static class PackageMetadataSerializer
    {
        /// <summary>
        /// Converts a TypeSymbol to a Go-like type string for serialization.
        /// Types from the same package use unqualified names; cross-package types are qualified.
        /// </summary>
        public static string TypeToString(TypeSymbol type, string? currentPackagePath = null)
        {
            if (type == null) return "interface{}";
            if (type.TypeKind == TypeKind.Error) return "interface{}";

            return type switch
            {
                SliceTypeSymbol slice => $"[]{TypeToString(slice.ElementType, currentPackagePath)}",
                ArrayTypeSymbol array => $"[{array.Length}]{TypeToString(array.ElementType, currentPackagePath)}",
                MapTypeSymbol map => $"map[{TypeToString(map.KeyType, currentPackagePath)}]{TypeToString(map.ValueType, currentPackagePath)}",
                PointerTypeSymbol ptr => $"*{TypeToString(ptr.ElementType, currentPackagePath)}",
                ChannelTypeSymbol chan => $"chan {TypeToString(chan.ElementType, currentPackagePath)}",
                FunctionTypeSymbol funcType => SerializeFuncType(funcType, currentPackagePath),
                InstantiatedTypeSymbol inst => SerializeInstantiatedType(inst, currentPackagePath),
                InterfaceTypeSymbol iface when iface == BuiltinTypes.EmptyInterface || iface.Methods.Count == 0 => "interface{}",
                InterfaceTypeSymbol iface when iface == BuiltinTypes.Error
                    || (iface.Name == "error" && iface.Methods.Count == 1 && iface.Methods[0].Name == "Error") => "error",
                InterfaceTypeSymbol iface when !string.IsNullOrEmpty(iface.PackagePath)
                    && iface.PackagePath != currentPackagePath
                    => iface.PackagePath + ":" + iface.Name,
                TypeParameterSymbol tp => $"~{tp.Name}",
                StructTypeSymbol st when st.Name == "struct{}" => "struct{}",
                _ when !string.IsNullOrEmpty(type.PackagePath)
                    && type.PackagePath != currentPackagePath
                    => type.PackagePath + ":" + type.Name,
                _ => type.Name,
            };
        }

        private static string SerializeInstantiatedType(InstantiatedTypeSymbol inst, string? currentPackagePath = null)
        {
            // Serialize as "BaseType«Arg1,Arg2»" using guillemets to avoid ambiguity with []
            var baseName = !string.IsNullOrEmpty(inst.GenericType.PackagePath)
                && inst.GenericType.PackagePath != currentPackagePath
                ? inst.GenericType.PackagePath + ":" + inst.GenericType.Name
                : inst.GenericType.Name;

            var args = new string[inst.TypeArguments.Count];
            for (int i = 0; i < inst.TypeArguments.Count; i++)
            {
                args[i] = TypeToString(inst.TypeArguments[i], currentPackagePath);
            }

            return baseName + "«" + string.Join(",", args) + "»";
        }

        private static string SerializeFuncType(FunctionTypeSymbol funcType, string? currentPackagePath = null)
        {
            var parts = new List<string>();
            for (int i = 0; i < funcType.ParameterTypes.Count; i++)
            {
                var paramType = funcType.ParameterTypes[i];
                if (funcType.IsVariadic && i == funcType.ParameterTypes.Count - 1
                    && paramType is SliceTypeSymbol variadicSlice)
                {
                    parts.Add("..." + TypeToString(variadicSlice.ElementType, currentPackagePath));
                }
                else
                {
                    parts.Add(TypeToString(paramType, currentPackagePath));
                }
            }
            var paramStr = string.Join(", ", parts);

            if (funcType.ReturnTypes.Count == 0)
            {
                return $"func({paramStr})";
            }
            if (funcType.ReturnTypes.Count == 1)
            {
                return $"func({paramStr}) {TypeToString(funcType.ReturnTypes[0], currentPackagePath)}";
            }

            var retParts = new List<string>();
            foreach (var r in funcType.ReturnTypes)
            {
                retParts.Add(TypeToString(r, currentPackagePath));
            }
            return $"func({paramStr}) ({string.Join(", ", retParts)})";
        }

        /// <summary>
        /// Parses a Go-like type string back into a TypeSymbol.
        /// </summary>
        public static TypeSymbol StringToType(string typeStr, Dictionary<string, TypeSymbol>? knownTypes = null,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            if (string.IsNullOrEmpty(typeStr))
                return BuiltinTypes.EmptyInterface;

            // Empty struct
            if (typeStr == "struct{}")
            {
                return new StructTypeSymbol("struct{}", System.Array.Empty<FieldSymbol>());
            }

            // Builtin types
            var builtin = BuiltinTypes.Resolve(typeStr);
            if (builtin != null) return builtin;

            // Known package-local types (try full name first)
            if (knownTypes != null)
            {
                if (knownTypes.TryGetValue(typeStr, out var known))
                    return known;
            }

            // Structural type checks BEFORE cross-package/unqualified lookups
            // to avoid "func() hash.Hash" matching local "Hash" via the dot-based heuristic.

            // Type parameter (~T): resolved to the declaring type's parameter via the scoped type
            // map (seeded with "~Name" entries by the readers, checked above). Reaching here means a
            // type-parameter reference appeared with no declaring scope — a serialization bug, not a
            // case to paper over with a fabricated ordinal-0 parameter.
            if (typeStr.StartsWith("~"))
            {
                throw new System.InvalidOperationException(
                    $"PackageMetadataSerializer: type parameter reference '{typeStr}' has no declaring " +
                    "type parameter in scope. The owning type/function failed to seed its parameters.");
            }

            // Slice ([]T)
            if (typeStr.StartsWith("[]"))
                return new SliceTypeSymbol(StringToType(typeStr.Substring(2), knownTypes, crossPkgResolver));

            // Pointer (*T)
            if (typeStr.StartsWith("*"))
                return new PointerTypeSymbol(StringToType(typeStr.Substring(1), knownTypes, crossPkgResolver));

            // Channel (<-chan T, chan<- T, chan T)
            if (typeStr.StartsWith("<-chan "))
                return new ChannelTypeSymbol(StringToType(typeStr.Substring(7), knownTypes, crossPkgResolver));
            if (typeStr.StartsWith("chan<- "))
                return new ChannelTypeSymbol(StringToType(typeStr.Substring(7), knownTypes, crossPkgResolver));
            if (typeStr.StartsWith("chan "))
                return new ChannelTypeSymbol(StringToType(typeStr.Substring(5), knownTypes, crossPkgResolver));

            // Array ([N]T)
            if (typeStr.StartsWith("[") && !typeStr.StartsWith("[]"))
            {
                var closeBracket = typeStr.IndexOf(']');
                if (closeBracket > 1 && int.TryParse(typeStr.Substring(1, closeBracket - 1), out int len))
                    return new ArrayTypeSymbol(StringToType(typeStr.Substring(closeBracket + 1), knownTypes, crossPkgResolver), len);
            }

            // Map (map[K]V)
            if (typeStr.StartsWith("map["))
            {
                var mapParts = ParseMapType(typeStr, knownTypes, crossPkgResolver);
                if (mapParts.KeyType != null && mapParts.ValueType != null)
                {
                    return new MapTypeSymbol(mapParts.KeyType, mapParts.ValueType);
                }
            }

            // Function type (func(...) ...)
            if (typeStr.StartsWith("func("))
                return ParseFuncType(typeStr, knownTypes, crossPkgResolver);

            // Instantiated generic type: "BaseType«Arg1,Arg2»" or "pkg:BaseType«Arg1,Arg2»"
            if (typeStr.Contains('«'))
            {
                var guilIdx = typeStr.IndexOf('«');
                var baseStr = typeStr.Substring(0, guilIdx);
                var argsStr = typeStr.Substring(guilIdx + 1).TrimEnd('»');

                var baseType = StringToType(baseStr, knownTypes, crossPkgResolver);
                if (baseType != null)
                {
                    var argParts = SplitTypeArgs(argsStr);
                    var typeArgs = new List<TypeSymbol>();
                    foreach (var argStr in argParts)
                    {
                        typeArgs.Add(StringToType(argStr.Trim(), knownTypes, crossPkgResolver)
                            ?? BuiltinTypes.EmptyInterface);
                    }
                    return new InstantiatedTypeSymbol(baseType, typeArgs);
                }
            }

            // Fully-qualified cross-package type: "github.com/foo/bar:TypeName"
            if (crossPkgResolver != null && typeStr.Contains(':'))
            {
                var colonIdx = typeStr.LastIndexOf(':');
                var importPath = typeStr.Substring(0, colonIdx);
                var typeName = typeStr.Substring(colonIdx + 1);
                var resolved = crossPkgResolver(importPath, typeName);
                if (resolved != null) return resolved;
            }

            // Cross-package type with dot separator: "pkg.TypeName" or "path/pkg.TypeName"
            // Reject CLR namespaces like "System.Collections.Generic.List" (multiple dots).
            // Accept short Go names ("big.Int") and full import paths ("math/big.Int").
            if (crossPkgResolver != null && typeStr.Contains('.'))
            {
                var dotIdx = typeStr.LastIndexOf('.');
                var pkgName = typeStr.Substring(0, dotIdx);
                var typeName = typeStr.Substring(dotIdx + 1);
                if (!pkgName.Contains('.'))
                {
                    var resolved = crossPkgResolver(pkgName, typeName);
                    if (resolved != null) return resolved;
                }
            }

            // Unqualified fallback: try short name in local type map
            if (knownTypes != null && typeStr.Contains('.'))
            {
                var dot = typeStr.LastIndexOf('.');
                var unqualified = typeStr.Substring(dot + 1);
                if (knownTypes.TryGetValue(unqualified, out var known2))
                    return known2;
            }

            // Unknown — return as an empty struct
            return new StructTypeSymbol(typeStr, System.Array.Empty<FieldSymbol>());
        }

        private static List<string> SplitTypeArgs(string argsStr)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < argsStr.Length; i++)
            {
                if (argsStr[i] == '«' || argsStr[i] == '[' || argsStr[i] == '(')
                {
                    depth++;
                }
                else if (argsStr[i] == '»' || argsStr[i] == ']' || argsStr[i] == ')')
                {
                    depth--;
                }
                else if (argsStr[i] == ',' && depth == 0)
                {
                    result.Add(argsStr.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < argsStr.Length)
            {
                result.Add(argsStr.Substring(start));
            }
            return result;
        }

        private static MapTypeParts ParseMapType(string typeStr, Dictionary<string, TypeSymbol>? knownTypes,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            int depth = 0;
            int keyStart = 4; // after "map["
            int keyEnd = -1;
            for (int i = keyStart; i < typeStr.Length; i++)
            {
                if (typeStr[i] == '[')
                {
                    depth++;
                }
                else if (typeStr[i] == ']')
                {
                    if (depth == 0)
                    {
                        keyEnd = i;
                        break;
                    }
                    depth--;
                }
            }
            if (keyEnd < 0)
            {
                return new MapTypeParts(null, null);
            }

            var keyStr = typeStr.Substring(keyStart, keyEnd - keyStart);
            var valStr = typeStr.Substring(keyEnd + 1);
            return new MapTypeParts(
                StringToType(keyStr, knownTypes, crossPkgResolver),
                StringToType(valStr, knownTypes, crossPkgResolver));
        }

        private static TypeSymbol ParseFuncType(string typeStr, Dictionary<string, TypeSymbol>? knownTypes,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            int parenStart = typeStr.IndexOf('(');
            if (parenStart < 0) return BuiltinTypes.EmptyInterface;

            int depth = 0;
            int parenEnd = -1;
            for (int i = parenStart; i < typeStr.Length; i++)
            {
                if (typeStr[i] == '(') depth++;
                else if (typeStr[i] == ')')
                {
                    depth--;
                    if (depth == 0) { parenEnd = i; break; }
                }
            }
            if (parenEnd < 0) return BuiltinTypes.EmptyInterface;

            var paramStr = typeStr.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
            var paramTypes = new List<TypeSymbol>();
            bool isVariadic = false;
            if (!string.IsNullOrEmpty(paramStr))
            {
                var paramParts = SplitTypeList(paramStr);
                // Two-pass approach to handle grouped params like "network, address string".
                // After comma-split: ["network", "address string"].
                // StripParamName("address string") → "string" (name was stripped).
                // StripParamName("network") → "network" (no space, unchanged).
                // The bare "network" is a grouped param name that borrows the type from the next part.
                // Key invariant: a bare part is a grouped name only if the NEXT part had its name
                // stripped (i.e., the next original DID contain a space and was changed by StripParamName).
                // This avoids misidentifying type-only params like "ResponseWriter" as grouped names.
                var trimmedOriginals = new string[paramParts.Count];
                var strippedParts = new string[paramParts.Count];
                for (int i = 0; i < paramParts.Count; i++)
                {
                    trimmedOriginals[i] = paramParts[i].Trim();
                    strippedParts[i] = StripParamName(trimmedOriginals[i]);
                }
                // Propagate types backward for grouped param names.
                for (int i = paramParts.Count - 2; i >= 0; i--)
                {
                    bool thisPartIsUnchangedBareWord = strippedParts[i] == trimmedOriginals[i]
                        && IsBareIdentifier(strippedParts[i]);
                    bool nextPartHadNameStripped = strippedParts[i + 1] != trimmedOriginals[i + 1];
                    if (thisPartIsUnchangedBareWord && nextPartHadNameStripped)
                    {
                        strippedParts[i] = strippedParts[i + 1];
                    }
                }
                for (int i = 0; i < strippedParts.Length; i++)
                {
                    var paramPart = strippedParts[i].Trim();
                    if (paramPart.StartsWith("..."))
                    {
                        isVariadic = true;
                        var elementType = StringToType(paramPart.Substring(3), knownTypes, crossPkgResolver);
                        paramTypes.Add(new SliceTypeSymbol(elementType));
                    }
                    else
                    {
                        paramTypes.Add(StringToType(paramPart, knownTypes, crossPkgResolver));
                    }
                }
            }

            var returnStr = typeStr.Substring(parenEnd + 1).Trim();
            var returnTypes = new List<TypeSymbol>();
            if (!string.IsNullOrEmpty(returnStr))
            {
                if (returnStr.StartsWith("(") && returnStr.EndsWith(")"))
                {
                    returnStr = returnStr.Substring(1, returnStr.Length - 2);
                }
                foreach (var r in SplitTypeList(returnStr))
                {
                    returnTypes.Add(StringToType(r.Trim(), knownTypes, crossPkgResolver));
                }
            }

            return new FunctionTypeSymbol(paramTypes, returnTypes, isVariadic);
        }

        /// <summary>
        /// Returns true if the string is a bare identifier (a single Go identifier with no type annotation).
        /// Such bare parts appear in grouped params like "network, address string" after splitting:
        /// "network" is a bare identifier that needs to inherit the type from the following param.
        /// </summary>
        private static bool IsBareIdentifier(string part)
        {
            if (string.IsNullOrEmpty(part)) return false;
            if (part.StartsWith("...") || part.StartsWith("*") || part.StartsWith("func(")
                || part.StartsWith("map[") || part.StartsWith("chan ") || part.StartsWith("<-chan ")
                || part.StartsWith("[") || part.Contains(".") || part.Contains(" ") || part.Contains("("))
            {
                return false;
            }
            return true;
        }

        private static string StripParamName(string paramPart)
        {
            var spaceIdx = paramPart.IndexOf(' ');
            if (spaceIdx > 0 && !paramPart.StartsWith("func(") && !paramPart.StartsWith("map[")
                && !paramPart.StartsWith("chan ") && !paramPart.StartsWith("<-chan "))
            {
                var beforeSpace = paramPart.Substring(0, spaceIdx);
                if (!beforeSpace.Contains('[') && !beforeSpace.Contains('*')
                    && !beforeSpace.Contains('.') && !beforeSpace.Contains('('))
                {
                    return paramPart.Substring(spaceIdx + 1).Trim();
                }
            }
            return paramPart;
        }

        private static string PackagePathLastSegment(string packagePath)
        {
            return Ngo.Compiler.Semantics.CompilationContext.GetDefaultPackageName(packagePath);
        }

        private static List<string> SplitTypeList(string s)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(' || s[i] == '[' || s[i] == '\u00AB') depth++;
                else if (s[i] == ')' || s[i] == ']' || s[i] == '\u00BB') depth--;
                else if (s[i] == ',' && depth == 0)
                {
                    result.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < s.Length)
                result.Add(s.Substring(start));
            return result;
        }
    }
}
