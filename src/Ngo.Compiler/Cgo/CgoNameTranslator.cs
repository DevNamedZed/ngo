// -----------------------------------------------------------------------
// <copyright file="CgoNameTranslator.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Translates Go-side cgo identifier conventions into the matching
    /// C expression. Go's cgo spec reserves three prefixes for C tag
    /// namespaces that Go's syntax cannot spell directly:
    /// <list type="bullet">
    ///   <item><c>C.struct_X</c> refers to C's <c>struct X</c></item>
    ///   <item><c>C.union_X</c>  refers to C's <c>union X</c></item>
    ///   <item><c>C.enum_X</c>   refers to C's <c>enum X</c></item>
    /// </list>
    /// All other names pass through unchanged because they name typedefs,
    /// functions, macros, or variables that already live in C's ordinary
    /// identifier namespace.
    /// </summary>
    public static class CgoNameTranslator
    {
        private const string StructPrefix = "struct_";
        private const string UnionPrefix = "union_";
        private const string EnumPrefix = "enum_";

        /// <summary>
        /// Convert a Go-side cgo identifier into the C expression that
        /// refers to the same entity. Only the three tag-namespace
        /// prefixes defined by Go's cgo spec are translated; all other
        /// names are returned as-is so that typedefs, functions, macros,
        /// and variables pass through without modification.
        /// </summary>
        public static string ToCExpression(string goCgoName)
        {
            if (goCgoName.StartsWith(StructPrefix, System.StringComparison.Ordinal))
            {
                return "struct " + goCgoName.Substring(StructPrefix.Length);
            }
            if (goCgoName.StartsWith(UnionPrefix, System.StringComparison.Ordinal))
            {
                return "union " + goCgoName.Substring(UnionPrefix.Length);
            }
            if (goCgoName.StartsWith(EnumPrefix, System.StringComparison.Ordinal))
            {
                return "enum " + goCgoName.Substring(EnumPrefix.Length);
            }
            return goCgoName;
        }
    }
}
