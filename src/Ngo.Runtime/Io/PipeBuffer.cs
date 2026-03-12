// -----------------------------------------------------------------------
// <copyright file="PipeBuffer.cs" company="Ziad">
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

namespace Ngo.Runtime.Io
{
    /// <summary>Shared buffer for io.Pipe.</summary>
    internal sealed class PipeBuffer
    {
        private readonly object _lock = new object();
        private byte[]? _data;
        private bool _closed;
        private string _closeErr = "";

        public (int, string) Write(Slice<byte> p)
        {
            lock (_lock)
            {
                if (_closed)
                    return (0, GoIo.ErrClosedPipe);
                _data = new byte[p.Len];
                for (int i = 0; i < p.Len; i++)
                    _data[i] = p[i];
                System.Threading.Monitor.PulseAll(_lock);
                return (p.Len, "");
            }
        }

        public (int, string) Read(Slice<byte> p)
        {
            lock (_lock)
            {
                while (_data == null && !_closed)
                    System.Threading.Monitor.Wait(_lock);

                if (_data == null && _closed)
                    return (0, _closeErr != "" ? _closeErr : GoIo.EOF);

                int n = global::System.Math.Min(p.Len, _data!.Length);
                for (int i = 0; i < n; i++)
                    p[i] = _data[i];
                _data = null;
                return (n, "");
            }
        }

        public void Close(string err)
        {
            lock (_lock)
            {
                _closed = true;
                _closeErr = err;
                System.Threading.Monitor.PulseAll(_lock);
            }
        }
    }
}
