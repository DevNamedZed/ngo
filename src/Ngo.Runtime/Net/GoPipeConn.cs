// -----------------------------------------------------------------------
// <copyright file="GoPipeConn.cs" company="Ziad">
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

namespace Ngo.Runtime.Net
{
    // Pipe connection stub
    internal class GoPipeConn : IGoNetConn
    {
        public (int, string) Read(Slice<byte> b) => (0, "EOF");
        public (int, string) Write(Slice<byte> b) => (b.Len, null!);
        public string Close() => null!;
        public IGoNetAddr LocalAddr() => new GoTCPAddr();
        public IGoNetAddr RemoteAddr() => new GoTCPAddr();
        public string SetDeadline(object t) => null!;
        public string SetReadDeadline(object t) => null!;
        public string SetWriteDeadline(object t) => null!;
    }
}
