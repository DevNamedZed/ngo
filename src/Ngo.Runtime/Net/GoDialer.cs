// -----------------------------------------------------------------------
// <copyright file="GoDialer.cs" company="Ziad">
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
    [GoType("struct", Name = "Dialer", Package = "net")]
    public class GoDialer
    {
        [GoField(Name = "Timeout")] public long Timeout { get; set; }
        [GoField(Name = "Deadline")] public object? Deadline { get; set; }
        [GoField(Name = "KeepAlive")] public long KeepAlive { get; set; }
        [GoField(Name = "LocalAddr")] public object? LocalAddr { get; set; }
        [GoField(Name = "Resolver")] public object? Resolver { get; set; }
        [GoField(Name = "FallbackDelay")] public long FallbackDelay { get; set; }
        [GoField(Name = "Control")] public object? Control { get; set; }
        [GoField(Name = "DualStack")] public bool DualStack { get; set; }
        [GoField(Name = "ControlContext")] public object? ControlContext { get; set; }

        [GoMethod]
        [return: GoReturn("Conn", "error")]
        public (object?, object?) Dial(string network, string address) => GoNet.Dial(network, address);

        [GoMethod]
        [return: GoReturn("Conn", "error")]
        public (object?, object?) DialContext(object? ctx, string network, string address) => GoNet.Dial(network, address);
    }
}
