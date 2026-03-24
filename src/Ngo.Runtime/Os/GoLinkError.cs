// -----------------------------------------------------------------------
// <copyright file="GoLinkError.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os
{
    /// <summary>
    /// Represents Go's os.LinkError struct.
    /// </summary>
    [GoType("struct", Name = "LinkError", Package = "os")]
    public sealed class GoLinkError
    {
        [GoField(Name = "Op")]
        public string Op { get; }

        [GoField(Name = "Old")]
        public string Old { get; }

        [GoField(Name = "New")]
        public string New { get; }

        [GoField(Name = "Err", Type = "error")]
        public object Err { get; }

        public GoLinkError(string op, string old, string @new, object err)
        {
            Op = op;
            Old = old;
            New = @new;
            Err = err;
        }

        [GoMethod]
        public string Error() => $"{Op} {Old} {New}: {Err}";

        public override string ToString() => Error();
    }
}
