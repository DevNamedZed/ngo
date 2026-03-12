// -----------------------------------------------------------------------
// <copyright file="GoProcAttr.cs" company="Ziad">
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
using Ngo.Runtime;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os
{
    /// <summary>
    /// Represents Go's os.ProcAttr struct.
    /// </summary>
    [GoType("struct", Name = "ProcAttr", Package = "os")]
    public sealed class GoProcAttr
    {
        [GoField(Name = "Dir")]
        public string Dir { get; set; } = "";
        [GoField(Name = "Env")]
        public Slice<string> Env { get; set; }
        [GoField(Name = "Files")]
        public object? Files { get; set; }
        [GoField(Name = "Sys")]
        public object? Sys { get; set; }

        public GoProcAttr()
        {
            Env = new Slice<string>(Array.Empty<string>());
        }
    }
}
