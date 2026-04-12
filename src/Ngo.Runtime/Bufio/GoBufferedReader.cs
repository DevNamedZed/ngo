// -----------------------------------------------------------------------
// <copyright file="GoBufferedReader.cs" company="Ziad">
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
    public sealed class GoBufferedReader
    {
        private readonly IGoReader _reader;
        private readonly byte[] _internalBuffer;
        private int _bufferStart;
        private int _bufferEnd;

        private const int DefaultBufferSize = 4096;

        public GoBufferedReader(IGoReader reader)
        {
            _reader = reader;
            _internalBuffer = new byte[DefaultBufferSize];
            _bufferStart = 0;
            _bufferEnd = 0;
        }

        public (long, string) Read(Slice<byte> destination)
        {
            if (_bufferStart < _bufferEnd)
            {
                int available = _bufferEnd - _bufferStart;
                int toCopy = System.Math.Min(available, destination.Len);
                for (int i = 0; i < toCopy; i++)
                {
                    destination[i] = _internalBuffer[_bufferStart + i];
                }
                _bufferStart += toCopy;
                return (toCopy, "");
            }

            if (destination.Len >= _internalBuffer.Length)
            {
                return _reader.Read(destination);
            }

            var bufSlice = new Slice<byte>(_internalBuffer);
            var (bytesRead, err) = _reader.Read(bufSlice);
            if (bytesRead == 0)
            {
                return (0, err);
            }

            _bufferStart = 0;
            _bufferEnd = (int)bytesRead;

            int count = System.Math.Min(_bufferEnd, destination.Len);
            for (int i = 0; i < count; i++)
            {
                destination[i] = _internalBuffer[i];
            }
            _bufferStart = count;
            return (count, "");
        }

        public (string, string) ReadString(byte delimiter)
        {
            var result = new List<byte>();

            while (true)
            {
                if (_bufferStart < _bufferEnd)
                {
                    for (int i = _bufferStart; i < _bufferEnd; i++)
                    {
                        result.Add(_internalBuffer[i]);
                        if (_internalBuffer[i] == delimiter)
                        {
                            _bufferStart = i + 1;
                            return (Encoding.UTF8.GetString(result.ToArray()), "");
                        }
                    }
                    _bufferStart = _bufferEnd;
                }

                var bufSlice = new Slice<byte>(_internalBuffer);
                var (bytesRead, err) = _reader.Read(bufSlice);
                _bufferStart = 0;
                _bufferEnd = (int)bytesRead;

                if (bytesRead == 0)
                {
                    if (result.Count > 0)
                    {
                        return (Encoding.UTF8.GetString(result.ToArray()), err);
                    }
                    return ("", err);
                }
            }
        }
    }
}
