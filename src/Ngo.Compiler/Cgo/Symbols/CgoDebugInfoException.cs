// -----------------------------------------------------------------------
// <copyright file="CgoDebugInfoException.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// Thrown when the semantic layer cannot translate DWARF debug
    /// info into a usable <c>CgoSymbolCatalog</c> entry. Wraps
    /// lower-layer exceptions (<c>DwarfParseException</c>,
    /// <c>ObjectFileException</c>, <c>BinaryReadException</c>) so
    /// the outer build driver catches a single type. Carries the
    /// originating C symbol name when available — matching the
    /// hardening rule that every diagnostic identifies which
    /// requested <c>C.&lt;name&gt;</c> drove the failing walk.
    /// </summary>
    public sealed class CgoDebugInfoException : Exception
    {
        public CgoDebugInfoException(string message)
            : base(message)
        {
        }

        public CgoDebugInfoException(string message, string requestedSymbolName)
            : base(message)
        {
            RequestedSymbolName = requestedSymbolName;
        }

        public CgoDebugInfoException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public CgoDebugInfoException(
            string message, string requestedSymbolName, Exception innerException)
            : base(message, innerException)
        {
            RequestedSymbolName = requestedSymbolName;
        }

        /// <summary>
        /// Name of the requested C symbol (for example, the
        /// <c>C.sqlite3_backup</c> reference that triggered this walk),
        /// if the exception originated inside a symbol lookup. Null
        /// when the failure happened before a symbol was selected.
        /// </summary>
        public string? RequestedSymbolName { get; }
    }
}
