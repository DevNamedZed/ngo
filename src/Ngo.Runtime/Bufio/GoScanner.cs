// -----------------------------------------------------------------------
// <copyright file="GoScanner.cs" company="Ziad">
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

using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Io;

namespace Ngo.Runtime
{
    public sealed class GoScanner
    {
        private readonly IGoReader _reader;
        private readonly List<byte> _buffer;
        private string _token;
        private bool _done;

        public GoScanner(IGoReader reader)
        {
            _reader = reader;
            _buffer = new List<byte>();
            _token = "";
            _done = false;
        }

        public bool Scan()
        {
            if (_done)
            {
                return false;
            }

            _buffer.Clear();
            var singleByte = new Slice<byte>(new byte[1]);

            while (true)
            {
                var (bytesRead, err) = _reader.Read(singleByte);
                if (bytesRead > 0)
                {
                    byte current = singleByte[0];
                    if (current == (byte)'\n')
                    {
                        _token = Encoding.UTF8.GetString(_buffer.ToArray());
                        return true;
                    }
                    _buffer.Add(current);
                }

                if (err == GoIo.EOF)
                {
                    _done = true;
                    if (_buffer.Count > 0)
                    {
                        _token = Encoding.UTF8.GetString(_buffer.ToArray());
                        return true;
                    }
                    return false;
                }

                if (err != "")
                {
                    _done = true;
                    return false;
                }
            }
        }

        public string Text()
        {
            return _token;
        }
    }
}
