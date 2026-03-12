// -----------------------------------------------------------------------
// <copyright file="SectionReader.cs" company="Ziad">
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

namespace Ngo.Runtime.Io
{
    /// <summary>io.SectionReader - reads from a section of a ReaderAt.</summary>
    [GoType("struct", Package = "io", Name = "SectionReader")]
    public sealed class SectionReader : IGoReader, IGoReaderAt, IGoSeeker
    {
        private readonly IGoReaderAt _r;
        private readonly long _base;
        private readonly long _limit;
        private long _off;

        internal SectionReader(IGoReaderAt r, long off, long n)
        {
            _r = r;
            _base = off;
            _off = off;
            _limit = off + n;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Read(Slice<byte> p)
        {
            if (_off >= _limit)
                return (0, GoIo.EOF);
            long maxRead = _limit - _off;
            if (p.Len > maxRead)
                p = p.Reslice(0, (int)maxRead);
            var (n, err) = _r.ReadAt(p, _off);
            _off += n;
            return (n, err);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) ReadAt(Slice<byte> p, long off)
        {
            if (off < 0 || off >= _limit - _base)
                return (0, GoIo.EOF);
            off += _base;
            long maxRead = _limit - off;
            if (p.Len > maxRead)
            {
                p = p.Reslice(0, (int)maxRead);
                var (n, _) = _r.ReadAt(p, off);
                return (n, GoIo.EOF);
            }
            return _r.ReadAt(p, off);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, string) Seek(long offset, [GoParam("int")] long whence)
        {
            long newOff;
            switch (whence)
            {
                case 0: // SeekStart
                    newOff = _base + offset;
                    break;
                case 1: // SeekCurrent
                    newOff = _off + offset;
                    break;
                case 2: // SeekEnd
                    newOff = _limit + offset;
                    break;
                default:
                    return (0, "io.SectionReader.Seek: invalid whence");
            }
            if (newOff < _base)
                return (0, "io.SectionReader.Seek: negative position");
            _off = newOff;
            return (newOff - _base, "");
        }

        [GoMethod]
        public long Size()
        {
            return _limit - _base;
        }
    }
}
