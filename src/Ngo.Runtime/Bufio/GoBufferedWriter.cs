// -----------------------------------------------------------------------
// <copyright file="GoBufferedWriter.cs" company="Ziad">
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
using Ngo.Runtime.Io;

namespace Ngo.Runtime
{
    public sealed class GoBufferedWriter
    {
        private readonly IGoWriter _writer;
        private readonly byte[] _internalBuffer;
        private int _buffered;

        private const int DefaultBufferSize = 4096;

        public GoBufferedWriter(IGoWriter writer)
        {
            _writer = writer;
            _internalBuffer = new byte[DefaultBufferSize];
            _buffered = 0;
        }

        public (long, string) Write(Slice<byte> data)
        {
            int totalWritten = 0;

            while (totalWritten < data.Len)
            {
                int space = _internalBuffer.Length - _buffered;
                int toCopy = System.Math.Min(space, data.Len - totalWritten);

                for (int i = 0; i < toCopy; i++)
                {
                    _internalBuffer[_buffered + i] = data[totalWritten + i];
                }
                _buffered += toCopy;
                totalWritten += toCopy;

                if (_buffered == _internalBuffer.Length)
                {
                    var (_, err) = FlushInternal();
                    if (err != "")
                    {
                        return (totalWritten, err);
                    }
                }
            }

            return (totalWritten, "");
        }

        public string Flush()
        {
            var (_, err) = FlushInternal();
            return err;
        }

        private (long, string) FlushInternal()
        {
            if (_buffered == 0)
            {
                return (0, "");
            }

            var slice = new Slice<byte>(_internalBuffer, 0, _buffered);
            var (written, err) = _writer.Write(slice);
            if (written > 0)
            {
                int remaining = _buffered - (int)written;
                if (remaining > 0)
                {
                    Array.Copy(_internalBuffer, (int)written, _internalBuffer, 0, remaining);
                }
                _buffered = remaining;
            }
            return (written, err);
        }
    }
}
