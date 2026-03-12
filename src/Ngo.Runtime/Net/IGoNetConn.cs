// -----------------------------------------------------------------------
// <copyright file="IGoNetConn.cs" company="Ziad">
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
    [GoType("interface", Name = "Conn", Package = "net")]
    public interface IGoNetConn
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        (int, string) Read(Slice<byte> b);

        [GoMethod]
        [return: GoReturn("int", "error")]
        (int, string) Write(Slice<byte> b);

        [GoMethod]
        [return: GoReturn("error")]
        string Close();

        [GoMethod]
        IGoNetAddr LocalAddr();

        [GoMethod]
        IGoNetAddr RemoteAddr();

        [GoMethod]
        [return: GoReturn("error")]
        string SetDeadline(object t);

        [GoMethod]
        [return: GoReturn("error")]
        string SetReadDeadline(object t);

        [GoMethod]
        [return: GoReturn("error")]
        string SetWriteDeadline(object t);
    }
}
