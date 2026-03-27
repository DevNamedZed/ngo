// -----------------------------------------------------------------------
// <copyright file="GoInterface.cs" company="Ziad">
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

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "Interface", Package = "net")]
    public class GoInterface
    {
        [GoField(Name = "Index")] public long Index { get; set; }
        [GoField(Name = "MTU")] public long MTU { get; set; }
        [GoField(Name = "Name")] public string Name { get; set; } = "";
        [GoField(Name = "HardwareAddr")] public Slice<byte> HardwareAddr { get; set; }
        [GoField(Name = "Flags", Type = "net.Flags")] public long Flags { get; set; }

        [GoMethod]
        [return: GoReturn("[]Addr", "error")]
        public (Slice<string>, object?) Addrs() => (new Slice<string>(Array.Empty<string>()), null);
    }
}
