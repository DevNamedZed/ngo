// -----------------------------------------------------------------------
// <copyright file="GoOpError.cs" company="Ziad">
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

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "OpError", Package = "net")]
    public class GoOpError
    {
        [GoField(Name = "Op")] public string Op { get; set; } = "";
        [GoField(Name = "Net")] public string Net { get; set; } = "";
        [GoField(Name = "Source")] public object? Source { get; set; }
        [GoField(Name = "Addr")] public object? Addr { get; set; }
        [GoField(Name = "Err", Type = "error")] public object? Err { get; set; }

        [GoMethod]
        public string Error() => $"{Op} {Net}: {Err}";

        [GoMethod]
        public bool Timeout() => false;

        [GoMethod]
        public bool Temporary() => false;
    }
}
