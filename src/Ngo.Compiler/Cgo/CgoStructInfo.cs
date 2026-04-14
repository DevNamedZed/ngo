// -----------------------------------------------------------------------
// <copyright file="CgoStructInfo.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Immutable description of a C struct or union in the symbol
    /// catalog. Distinguishes struct from union via
    /// <see cref="IsUnion"/> and carries the resolved layout
    /// (<see cref="SizeBytes"/>, <see cref="AlignmentBytes"/>) so
    /// emission can honour the real ABI on the target platform.
    ///
    /// Fields are held in declaration order. Alignment of zero means
    /// the debug info did not advertise an explicit
    /// <c>DW_AT_alignment</c> (DWARF 5 attribute, optional); consumers
    /// should infer alignment from field sizes when that happens.
    /// </summary>
    public sealed class CgoStructInfo
    {
        public CgoStructInfo(
            string cName,
            string goName,
            IReadOnlyList<CgoFieldInfo> fields,
            bool isUnion,
            long sizeBytes,
            long alignmentBytes)
        {
            if (cName == null)
            {
                throw new ArgumentNullException(nameof(cName));
            }
            if (goName == null)
            {
                throw new ArgumentNullException(nameof(goName));
            }
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }
            if (sizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeBytes), sizeBytes, "Struct size must be non-negative.");
            }
            if (alignmentBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alignmentBytes), alignmentBytes, "Struct alignment must be non-negative.");
            }

            CName = cName;
            GoName = goName;
            Fields = fields;
            IsUnion = isUnion;
            SizeBytes = sizeBytes;
            AlignmentBytes = alignmentBytes;
        }

        public string CName { get; }

        public string GoName { get; }

        public IReadOnlyList<CgoFieldInfo> Fields { get; }

        public bool IsUnion { get; }

        public long SizeBytes { get; }

        public long AlignmentBytes { get; }
    }
}
