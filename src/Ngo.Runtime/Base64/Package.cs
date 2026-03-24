// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Base64
{
    [GoPackage("encoding/base64")]
    public static class Package
    {
        [GoConst]
        public const long StdPadding = '=';

        [GoConst]
        public const long NoPadding = -1;

        // encoding instances (exposed as package vars)
        public static readonly Encoding StdEncoding =
            new Encoding(Convert.ToBase64String, Convert.FromBase64String, false);
        public static readonly Encoding URLEncoding =
            new Encoding(EncodeURL, DecodeURL, true);
        public static readonly Encoding RawStdEncoding =
            new Encoding(s => Convert.ToBase64String(s).TrimEnd('='),
                s => Convert.FromBase64String(PadBase64(s)), false);
        public static readonly Encoding RawURLEncoding =
            new Encoding(s => EncodeURL(s).TrimEnd('='),
                s => DecodeURL(PadBase64(s)), true);

        public static Encoding NewEncoding(string encoder)
        {
            return new Encoding(
                data => Convert.ToBase64String(data),
                s => Convert.FromBase64String(s),
                false);
        }

        [return: GoReturn("io.WriteCloser")]
        public static object NewEncoder(Encoding enc, [GoParam("io.Writer")] object w)
        {
            return new Base64StreamEncoder(enc, w as Io.IGoWriter);
        }

        public static object NewDecoder(Encoding enc, [GoParam("io.Reader")] object r)
        {
            return r;
        }

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

    [GoType("named", Name = "CorruptInputError", Package = "encoding/base64", Underlying = "int64")]
    public class GoCorruptInputError
    {
        [GoField] public long Value;

        [GoMethod]
        public string Error() => $"illegal base64 data at input byte {Value}";
    }
}
