// -----------------------------------------------------------------------
// <copyright file="GoTCPConn.cs" company="Ziad">
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
using System.Net.Sockets;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "TCPConn", Package = "net")]
    public class GoTCPConn : IGoNetConn
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        public GoTCPConn() { }
        public GoTCPConn(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public (int, string) Read(Slice<byte> b)
        {
            try
            {
                var arr = new byte[b.Len];
                for (int i = 0; i < b.Len; i++) arr[i] = b[i];
                int n = _stream?.Read(arr, 0, arr.Length) ?? 0;
                return (n, null!);
            }
            catch (Exception ex) { return (0, ex.Message); }
        }

        public (int, string) Write(Slice<byte> b)
        {
            try
            {
                var arr = new byte[b.Len];
                for (int i = 0; i < b.Len; i++) arr[i] = b[i];
                _stream?.Write(arr, 0, arr.Length);
                return (b.Len, null!);
            }
            catch (Exception ex) { return (0, ex.Message); }
        }

        public string Close()
        {
            _client?.Close();
            return null!;
        }

        public IGoNetAddr LocalAddr() => new GoTCPAddr();
        public IGoNetAddr RemoteAddr() => new GoTCPAddr();
        public string SetDeadline(object t) => null!;
        public string SetReadDeadline(object t) => null!;
        public string SetWriteDeadline(object t) => null!;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) ReadFrom(object? r) => (0, null!);

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseWrite() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseRead() => null;
    }
}
