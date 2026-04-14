// -----------------------------------------------------------------------
// <copyright file="BinaryReaderLittleEndian.cs" company="Ziad">
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
using System.Text;

namespace Ngo.Compiler.Cgo.Binary
{
    /// <summary>
    /// Cursor-based little-endian reader over a <see cref="byte"/> array.
    /// Used by the ELF section reader and the DWARF parser to walk raw
    /// debug sections without materialising intermediate streams. All
    /// fixed-width integer reads are little-endian, which matches the
    /// ELF64 target of this stage; big-endian support would be a
    /// subclass, not a flag, if it is ever added.
    ///
    /// Reads that would pass the end of the buffer throw
    /// <see cref="BinaryReadException"/> carrying the byte offset at
    /// which the read began, so higher-level diagnostics can anchor to
    /// a section offset. The reader never silently returns a short
    /// value or a default on truncation.
    /// </summary>
    public sealed class BinaryReaderLittleEndian
    {
        private readonly byte[] _data;
        private int _position;

        public BinaryReaderLittleEndian(byte[] data)
            : this(data, 0)
        {
        }

        public BinaryReaderLittleEndian(byte[] data, int startOffset)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (startOffset < 0 || startOffset > data.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startOffset),
                    "Start offset " + startOffset +
                    " is outside the buffer range [0, " + data.Length + "].");
            }

            _data = data;
            _position = startOffset;
        }

        /// <summary>
        /// Current byte offset into the underlying buffer. The next
        /// read begins here.
        /// </summary>
        public int Position
        {
            get { return _position; }
        }

        /// <summary>
        /// Total length of the underlying buffer in bytes.
        /// </summary>
        public int Length
        {
            get { return _data.Length; }
        }

        /// <summary>
        /// Number of bytes remaining between the cursor and the end of
        /// the buffer.
        /// </summary>
        public int Remaining
        {
            get { return _data.Length - _position; }
        }

        /// <summary>
        /// Underlying buffer reference. Exposed so helpers that take
        /// <c>byte[] + offset</c> (e.g. <see cref="Leb128"/>) can decode
        /// without an intermediate copy. Callers must not mutate the
        /// buffer.
        /// </summary>
        public byte[] Data
        {
            get { return _data; }
        }

        /// <summary>
        /// Reposition the cursor to an absolute offset. Accepts the
        /// end-of-buffer position so a reader can legitimately exhaust
        /// the buffer and report zero remaining bytes.
        /// </summary>
        public void Seek(int position)
        {
            if (position < 0 || position > _data.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    "Seek offset " + position +
                    " is outside the buffer range [0, " + _data.Length + "].");
            }
            _position = position;
        }

        /// <summary>
        /// Advance the cursor by the given number of bytes. Negative
        /// counts are rejected; use <see cref="Seek"/> for absolute
        /// repositioning.
        /// </summary>
        public void Skip(int byteCount)
        {
            if (byteCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(byteCount),
                    "Skip count must be non-negative; got " + byteCount + ".");
            }
            if (byteCount > Remaining)
            {
                throw new BinaryReadException(
                    "Skip of " + byteCount + " bytes would pass end of buffer; " +
                    "only " + Remaining + " remain.",
                    _position);
            }
            _position += byteCount;
        }

        public byte ReadU8()
        {
            EnsureAvailable(1, "ReadU8");
            byte value = _data[_position];
            _position++;
            return value;
        }

        public ushort ReadU16()
        {
            EnsureAvailable(2, "ReadU16");
            ushort value = (ushort)(
                _data[_position]
                | (_data[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public uint ReadU32()
        {
            EnsureAvailable(4, "ReadU32");
            uint value =
                ((uint)_data[_position])
                | ((uint)_data[_position + 1] << 8)
                | ((uint)_data[_position + 2] << 16)
                | ((uint)_data[_position + 3] << 24);
            _position += 4;
            return value;
        }

        public ulong ReadU64()
        {
            EnsureAvailable(8, "ReadU64");
            ulong value =
                ((ulong)_data[_position])
                | ((ulong)_data[_position + 1] << 8)
                | ((ulong)_data[_position + 2] << 16)
                | ((ulong)_data[_position + 3] << 24)
                | ((ulong)_data[_position + 4] << 32)
                | ((ulong)_data[_position + 5] << 40)
                | ((ulong)_data[_position + 6] << 48)
                | ((ulong)_data[_position + 7] << 56);
            _position += 8;
            return value;
        }

        /// <summary>
        /// Read <paramref name="count"/> bytes into a new array and
        /// advance the cursor. A zero-length request returns an empty
        /// array without advancing.
        /// </summary>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    "ReadBytes count must be non-negative; got " + count + ".");
            }
            if (count == 0)
            {
                return Array.Empty<byte>();
            }
            EnsureAvailable(count, "ReadBytes");
            byte[] copy = new byte[count];
            Array.Copy(_data, _position, copy, 0, count);
            _position += count;
            return copy;
        }

        /// <summary>
        /// Read a UTF-8 string terminated by a <c>0x00</c> byte,
        /// advancing the cursor past the terminator. Throws when the
        /// buffer ends before a terminator is found — a missing
        /// terminator indicates truncated or corrupt input and must
        /// not produce a section-spanning bogus string.
        /// </summary>
        public string ReadNullTerminatedUtf8()
        {
            int stringStart = _position;
            int scanPosition = _position;
            while (scanPosition < _data.Length && _data[scanPosition] != 0)
            {
                scanPosition++;
            }
            if (scanPosition >= _data.Length)
            {
                throw new BinaryReadException(
                    "UTF-8 string at offset " + stringStart +
                    " has no null terminator before end of buffer.",
                    stringStart);
            }

            int byteCount = scanPosition - stringStart;
            string value = byteCount == 0
                ? string.Empty
                : Encoding.UTF8.GetString(_data, stringStart, byteCount);
            _position = scanPosition + 1;
            return value;
        }

        /// <summary>
        /// Read an unsigned LEB128 value and advance the cursor by the
        /// encoded byte count. Delegates to <see cref="Leb128.ReadUnsigned"/>.
        /// </summary>
        public ulong ReadUnsignedLeb128()
        {
            ulong value = Leb128.ReadUnsigned(_data, _position, out int consumed);
            _position += consumed;
            return value;
        }

        /// <summary>
        /// Read a signed LEB128 value and advance the cursor by the
        /// encoded byte count. Delegates to <see cref="Leb128.ReadSigned"/>.
        /// </summary>
        public long ReadSignedLeb128()
        {
            long value = Leb128.ReadSigned(_data, _position, out int consumed);
            _position += consumed;
            return value;
        }

        /// <summary>
        /// Read an unsigned LEB128 value and narrow to <see cref="int"/>,
        /// throwing if the value does not fit. For abbrev codes, unit
        /// offsets, and string indices that must address bytes inside a
        /// section and so cannot legally exceed <see cref="int.MaxValue"/>.
        /// </summary>
        public int ReadUnsignedLeb128AsInt32()
        {
            int value = Leb128.ReadUnsignedAsInt32(_data, _position, out int consumed);
            _position += consumed;
            return value;
        }

        private void EnsureAvailable(int byteCount, string operation)
        {
            if (Remaining < byteCount)
            {
                throw new BinaryReadException(
                    operation + " needs " + byteCount + " byte(s) but only " +
                    Remaining + " remain in the buffer.",
                    _position);
            }
        }
    }
}
