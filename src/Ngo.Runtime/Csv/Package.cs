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
        public static Reader NewReader(object r)
        {
            if (r is IGoReader reader)
                return new Reader(reader);
            throw new InvalidOperationException("csv.NewReader requires an io.Reader");
        }

        public static Writer NewWriter(object w)
        {
            if (w is IGoWriter writer)
                return new Writer(writer);
            throw new InvalidOperationException("csv.NewWriter requires an io.Writer");
        }
    }
}
