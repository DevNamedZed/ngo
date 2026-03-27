// -----------------------------------------------------------------------
// <copyright file="GoIP.cs" company="Ziad">
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
    [GoType("named", Name = "IP", Package = "net", Underlying = "[]byte")]
    public class GoIP
    {
        [GoMethod]
        public string String() => "";

        [GoMethod]
        [return: GoReturn("IP")]
        public object? To4() => null;

        [GoMethod]
        [return: GoReturn("IP")]
        public object? To16() => null;

        [GoMethod]
        public bool Equal(object? other) => false;

        [GoMethod]
        public bool IsLoopback() => false;

        [GoMethod]
        public bool IsUnspecified() => false;

        [GoMethod]
        public bool IsGlobalUnicast() => false;

        [GoMethod]
        public bool IsLinkLocalUnicast() => false;

        [GoMethod]
        public bool IsLinkLocalMulticast() => false;

        [GoMethod]
        public bool IsInterfaceLocalMulticast() => false;

        [GoMethod]
        public bool IsMulticast() => false;

        [GoMethod]
        [return: GoReturn("IP")]
        public object? Mask(object? mask) => null;

        [GoMethod]
        [return: GoReturn("IPMask")]
        public object? DefaultMask() => null;

        [GoMethod]
        [return: GoReturn("string")]
        public string MarshalText() => "";
    }
}
