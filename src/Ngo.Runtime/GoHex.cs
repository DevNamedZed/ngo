// -----------------------------------------------------------------------
// <copyright file="GoHex.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoHex
    {
        public static string EncodeToString(Slice<byte> src)
        {
            return Convert.ToHexString(src.AsReadOnlySpan().ToArray()).ToLowerInvariant();
        }

        public static (Slice<byte>, object?) DecodeString(string s)
        {
            try
            {
                var bytes = Convert.FromHexString(s);
                return (new Slice<byte>(bytes), null);
            }
            catch (FormatException ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        public static long EncodedLen(long n)
        {
            return n * 2;
        }

        public static long DecodedLen(long n)
        {
            return n / 2;
        }

        public static Slice<byte> Encode(Slice<byte> dst, Slice<byte> src)
        {
            var hex = EncodeToString(src);
            var bytes = System.Text.Encoding.ASCII.GetBytes(hex);
            for (int i = 0; i < bytes.Length && i < dst.Len; i++)
            {
                dst[i] = bytes[i];
            }
            return dst;
        }

        public static (long, object?) Decode(Slice<byte> dst, Slice<byte> src)
        {
            var hex = System.Text.Encoding.ASCII.GetString(src.AsReadOnlySpan().ToArray());
            try
            {
                var bytes = Convert.FromHexString(hex);
                for (int i = 0; i < bytes.Length && i < dst.Len; i++)
                {
                    dst[i] = bytes[i];
                }
                return ((long)bytes.Length, null);
            }
            catch (FormatException ex)
            {
                return (0L, ex.Message);
            }
        }
    }
}
