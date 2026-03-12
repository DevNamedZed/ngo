// -----------------------------------------------------------------------
// <copyright file="GoPathError.cs" company="Ziad">
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
    /// Represents Go's os.PathError struct.
    /// </summary>
    [GoType("struct", Name = "PathError", Package = "os")]
    public sealed class GoPathError
    {
        [GoField(Name = "Op")] public string Op;
        [GoField(Name = "Path")] public string Path;
        [GoField(Name = "Err", Type = "error")] public object Err;

        public GoPathError(string op, string path, object err)
        {
            Op = op;
            Path = path;
            Err = err;
        }

        [GoMethod]
        public string Error() => $"{Op} {Path}: {Err}";
        [GoMethod]
        [return: GoReturn("error")]
        public object Unwrap() => Err;

        public override string ToString() => Error();
    }
}
