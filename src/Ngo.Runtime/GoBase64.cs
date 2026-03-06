// -----------------------------------------------------------------------
// <copyright file="GoBase64.cs" company="Ziad">
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
    public static class GoBase64
    {
        // encoding instances (exposed as package vars)
        public static readonly GoBase64Encoding StdEncoding =
            new GoBase64Encoding(Convert.ToBase64String, Convert.FromBase64String, false);
        public static readonly GoBase64Encoding URLEncoding =
            new GoBase64Encoding(EncodeURL, DecodeURL, true);
        public static readonly GoBase64Encoding RawStdEncoding =
            new GoBase64Encoding(s => Convert.ToBase64String(s).TrimEnd('='),
                s => Convert.FromBase64String(PadBase64(s)), false);
        public static readonly GoBase64Encoding RawURLEncoding =
            new GoBase64Encoding(s => EncodeURL(s).TrimEnd('='),
                s => DecodeURL(PadBase64(s)), true);

        private static string EncodeURL(byte[] data)
        {
            return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_');
        }

        private static byte[] DecodeURL(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            s = PadBase64(s);
            return Convert.FromBase64String(s);
        }

        private static string PadBase64(string s)
        {
            switch (s.Length % 4)
            {
                case 2: return s + "==";
                case 3: return s + "=";
                default: return s;
            }
        }
    }

    public class GoBase64Encoding
    {
        private readonly Func<byte[], string> _encode;
        private readonly Func<string, byte[]> _decode;

        internal GoBase64Encoding(Func<byte[], string> encode, Func<string, byte[]> decode, bool isURL)
        {
            _encode = encode;
            _decode = decode;
        }

        public string EncodeToString(Slice<byte> src)
        {
            return _encode(src.AsReadOnlySpan().ToArray());
        }

        public (Slice<byte>, object?) DecodeString(string s)
        {
            try
            {
                var bytes = _decode(s);
                return (new Slice<byte>(bytes), null);
            }
            catch (FormatException ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }
    }
}
