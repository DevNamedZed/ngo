// -----------------------------------------------------------------------
// <copyright file="GoTCPListener.cs" company="Ziad">
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
    [GoType("struct", Name = "TCPListener", Package = "net")]
    public class GoTCPListener : IGoNetListener
    {
        private TcpListener? _listener;

        public GoTCPListener() { }
        public GoTCPListener(TcpListener listener) { _listener = listener; }

        public (object?, object?) Accept()
        {
            try
            {
                var client = _listener?.AcceptTcpClient();
                return (client != null ? new GoTCPConn(client) : null, null);
            }
            catch (Exception ex) { return (null, ex.Message); }
        }

        public string Close()
        {
            _listener?.Stop();
            return null!;
        }

        public IGoNetAddr Addr() => new GoTCPAddr();

        [GoMethod]
        [return: GoReturn("*net.TCPConn", "error")]
        public (object?, object?) AcceptTCP()
        {
            try
            {
                var client = _listener?.AcceptTcpClient();
                return (client != null ? new GoTCPConn(client) : null, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }
}
