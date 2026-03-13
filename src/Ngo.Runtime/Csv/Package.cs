// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Csv
{
    [GoPackage("encoding/csv")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*Reader")]
        public static Reader NewReader([GoParam("io.Reader")] object r)
        {
            if (r is IGoReader reader)
            {
                return new Reader(reader);
            }
            throw new InvalidOperationException("csv.NewReader requires an io.Reader");
        }

        [GoFunc]
        [return: GoReturn("*Writer")]
        public static Writer NewWriter([GoParam("io.Writer")] object w)
        {
            if (w is IGoWriter writer)
            {
                return new Writer(writer);
            }
            throw new InvalidOperationException("csv.NewWriter requires an io.Writer");
        }

        [GoVar]
        public static readonly string ErrBareQuote = "bare \" in non-quoted-field";

        [GoVar]
        public static readonly string ErrFieldCount = "wrong number of fields";

        [GoVar]
        public static readonly string ErrQuote = "extraneous or missing \" in quoted-field";

        [GoVar]
        public static readonly string ErrTrailingComma = "extra delimiter at end of line";
    }

    [GoType("struct", Name = "ParseError", Package = "encoding/csv")]
    public class ParseError
    {
        [GoField(Name = "StartLine")]
        public long StartLine;

        [GoField(Name = "Line")]
        public long Line;

        [GoField(Name = "Column")]
        public long Column;

        [GoField(Name = "Err")]
        public object? Err;

        [GoMethod]
        public string Error() => $"record on line {Line}; parse error on line {Line}, column {Column}: {Err}";
    }
}
