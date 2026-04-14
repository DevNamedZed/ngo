// -----------------------------------------------------------------------
// <copyright file="Leb128.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Binary
{
    /// <summary>
    /// LEB128 decoder used by the DWARF parser. LEB128 is DWARF's
    /// variable-length integer encoding: each byte carries seven bits
    /// of payload in bits 0-6 and a continuation flag in bit 7. The
    /// unsigned form zero-extends; the signed form sign-extends from
    /// bit 6 of the terminating byte.
    ///
    /// This decoder returns 64-bit results. Values wider than 64 bits
    /// and encodings that exceed the ten-byte maximum for a 64-bit
    /// value are rejected with <see cref="Leb128ParseException"/>, as
    /// are truncated inputs whose terminating byte is missing. The
    /// reference DWARF readers we looked at both truncate to 32 bits
    /// silently; keeping the full 64-bit range here avoids that class
    /// of data-loss bug reaching the rest of the pipeline.
    /// </summary>
    public static class Leb128
    {
        /// <summary>
        /// Maximum number of bytes a legal LEB128 encoding may occupy
        /// when the decoded value fits in 64 bits. Each byte carries
        /// seven payload bits; ten bytes give seventy bits of capacity,
        /// of which only the low sixty-four are usable. An eleventh
        /// byte is always an overflow signal.
        /// </summary>
        public const int MaxByteLengthFor64BitValue = 10;

        /// <summary>
        /// Decode an unsigned LEB128 integer. Writes the number of
        /// bytes consumed into <paramref name="consumed"/>. Throws
        /// <see cref="Leb128ParseException"/> when the input is
        /// truncated or the encoded value does not fit in 64 bits.
        /// The tenth byte, if reached, must have a payload of
        /// <c>0x00</c> or <c>0x01</c> and must not set the continuation
        /// bit — any other pattern indicates a value above
        /// <c>ulong.MaxValue</c>.
        /// </summary>
        public static ulong ReadUnsigned(byte[] data, int offset, out int consumed)
        {
            ulong result = 0;
            int shift = 0;
            int position = offset;

            for (int byteIndex = 0; byteIndex < MaxByteLengthFor64BitValue; byteIndex++)
            {
                if (position >= data.Length)
                {
                    throw new Leb128ParseException(
                        "LEB128 unsigned: input truncated after " + byteIndex +
                        " byte(s); continuation bit of prior byte was set.",
                        offset);
                }

                byte currentByte = data[position];
                position++;

                byte payload = (byte)(currentByte & 0x7F);
                bool continues = (currentByte & 0x80) != 0;

                if (byteIndex == MaxByteLengthFor64BitValue - 1)
                {
                    if (continues || payload > 0x01)
                    {
                        throw new Leb128ParseException(
                            "LEB128 unsigned: encoded value exceeds 64-bit range.",
                            offset);
                    }
                }

                result |= ((ulong)payload) << shift;

                if (!continues)
                {
                    consumed = position - offset;
                    return result;
                }

                shift += 7;
            }

            throw new Leb128ParseException(
                "LEB128 unsigned: encoded value exceeds 64-bit range.",
                offset);
        }

        /// <summary>
        /// Decode a signed LEB128 integer. Writes the number of bytes
        /// consumed into <paramref name="consumed"/>. Throws
        /// <see cref="Leb128ParseException"/> when the input is
        /// truncated or the encoded value does not fit in 64 bits.
        /// The tenth byte, if reached, must have a payload of
        /// <c>0x00</c> (sign-extension of a positive value whose
        /// bit 63 is zero) or <c>0x7F</c> (sign-extension of a
        /// negative value whose bit 63 is one) — any other pattern
        /// indicates a value outside the signed 64-bit range.
        /// </summary>
        public static long ReadSigned(byte[] data, int offset, out int consumed)
        {
            ulong magnitude = 0;
            int shift = 0;
            int position = offset;
            bool signBitSet = false;

            for (int byteIndex = 0; byteIndex < MaxByteLengthFor64BitValue; byteIndex++)
            {
                if (position >= data.Length)
                {
                    throw new Leb128ParseException(
                        "LEB128 signed: input truncated after " + byteIndex +
                        " byte(s); continuation bit of prior byte was set.",
                        offset);
                }

                byte currentByte = data[position];
                position++;

                byte payload = (byte)(currentByte & 0x7F);
                bool continues = (currentByte & 0x80) != 0;

                if (byteIndex == MaxByteLengthFor64BitValue - 1)
                {
                    if (continues || (payload != 0x00 && payload != 0x7F))
                    {
                        throw new Leb128ParseException(
                            "LEB128 signed: encoded value exceeds 64-bit range.",
                            offset);
                    }
                }

                magnitude |= ((ulong)payload) << shift;
                signBitSet = (payload & 0x40) != 0;

                if (!continues)
                {
                    long signedResult = unchecked((long)magnitude);
                    int signExtensionShift = shift + 7;
                    if (signBitSet && signExtensionShift < 64)
                    {
                        signedResult |= -1L << signExtensionShift;
                    }
                    consumed = position - offset;
                    return signedResult;
                }

                shift += 7;
            }

            throw new Leb128ParseException(
                "LEB128 signed: encoded value exceeds 64-bit range.",
                offset);
        }

        /// <summary>
        /// Convenience wrapper that decodes an unsigned LEB128 and
        /// throws when the result does not fit in a signed 32-bit
        /// integer. Used for fields the DWARF spec stores as ULEB128
        /// but restricts to values that must address bytes inside an
        /// in-memory section (abbrev codes, unit offsets, string-table
        /// indices).
        /// </summary>
        public static int ReadUnsignedAsInt32(byte[] data, int offset, out int consumed)
        {
            ulong raw = ReadUnsigned(data, offset, out consumed);
            if (raw > int.MaxValue)
            {
                throw new Leb128ParseException(
                    "LEB128 unsigned: value " + raw + " exceeds Int32 range.",
                    offset);
            }
            return (int)raw;
        }
    }
}
