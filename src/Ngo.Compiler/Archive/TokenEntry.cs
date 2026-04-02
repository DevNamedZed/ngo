// -----------------------------------------------------------------------
// <copyright file="TokenEntry.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// A structured token entry from IL metadata. Carries typed fields per token kind
    /// instead of a single opaque string, eliminating the need for string parsing at link time.
    ///
    /// Type tokens:   Reference = type name
    /// String tokens: Reference = string value
    /// Field tokens:  Reference = declaring type name, MemberName = field name
    /// Method tokens: Reference = declaring type name, MemberName = method name,
    ///                GenericTypeArgs = explicit type args, ParamTypes = parameter type names
    /// </summary>
    public sealed class TokenEntry
    {
        public TokenEntry(int offset, byte kind, string reference)
            : this(offset, kind, reference, "", Array.Empty<string>(), Array.Empty<string>())
        {
        }

        public TokenEntry(int offset, byte kind, string reference, string memberName,
            string[] genericTypeArgs, string[] paramTypes)
        {
            Offset = offset;
            Kind = kind;
            Reference = reference;
            MemberName = memberName;
            GenericTypeArgs = genericTypeArgs;
            ParamTypes = paramTypes;
        }

        public int Offset { get; }

        public byte Kind { get; }

        public string Reference { get; }

        public string MemberName { get; }

        public string[] GenericTypeArgs { get; }

        public string[] ParamTypes { get; }
    }
}
