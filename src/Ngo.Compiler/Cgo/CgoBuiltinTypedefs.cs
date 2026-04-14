// -----------------------------------------------------------------------
// <copyright file="CgoBuiltinTypedefs.cs" company="Ziad">
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
    /// The fixed set of C typedefs that <c>go tool cgo</c> auto-injects
    /// so Go code can spell C integer types whose natural C names contain
    /// spaces (<c>unsigned char</c>, <c>long long</c>, …). Without these,
    /// a Go file that references <c>C.uchar</c> or <c>C.longlong</c> will
    /// fail to compile against the user preamble alone, because those
    /// names do not exist in any standard C header.
    /// </summary>
    public static class CgoBuiltinTypedefs
    {
        /// <summary>
        /// The C source block injected ahead of the user preamble. Names
        /// and target types mirror <c>cmd/cgo</c> in the upstream Go
        /// toolchain so that packages that depend on Go cgo's
        /// documented conventions compile without modification.
        /// </summary>
        public static readonly string CSourceBlock =
            "/* Go cgo built-in typedefs: auto-injected by `go tool cgo` so Go code" + "\n" +
            " * can reference C integer types whose natural C names contain spaces. */" + "\n" +
            "typedef signed char schar;" + "\n" +
            "typedef unsigned char uchar;" + "\n" +
            "typedef unsigned short ushort;" + "\n" +
            "typedef unsigned int uint;" + "\n" +
            "typedef unsigned long ulong;" + "\n" +
            "typedef long long longlong;" + "\n" +
            "typedef unsigned long long ulonglong;" + "\n";
    }
}
