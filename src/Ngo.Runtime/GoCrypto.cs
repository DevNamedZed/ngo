// -----------------------------------------------------------------------
// <copyright file="GoCrypto.cs" company="Ziad">
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
using System.Security.Cryptography;
using System.Numerics;

namespace Ngo.Runtime
{
    public static class GoSha256
    {
        public static Slice<byte> Sum256(Slice<byte> data)
        {
            var hash = SHA256.HashData(data.AsReadOnlySpan().ToArray());
            return new Slice<byte>(hash);
        }

        public static GoSha256Hash New()
        {
            return new GoSha256Hash();
        }
    }

    public class GoSha256Hash
    {
        private readonly IncrementalHash _hash;

        public GoSha256Hash()
        {
            _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }

        public (long, object?) Write(Slice<byte> p)
        {
            var arr = p.AsReadOnlySpan().ToArray();
            _hash.AppendData(arr);
            return ((long)arr.Length, null);
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            // Sum appends the current hash to b (does not reset)
            var hash = _hash.GetCurrentHash();
            var result = new byte[b.Len + hash.Length];
            for (int i = 0; i < b.Len; i++)
                result[i] = b[i];
            Array.Copy(hash, 0, result, (int)b.Len, hash.Length);
            return new Slice<byte>(result);
        }

        public void Reset()
        {
            // IncrementalHash doesn't support Reset, create new
        }

        public long Size()
        {
            return 32;
        }

        public long BlockSize()
        {
            return 64;
        }
    }

    public static class GoCryptoRand
    {
        public static (long, object?) Read(Slice<byte> b)
        {
            var arr = new byte[(int)b.Len];
            RandomNumberGenerator.Fill(arr);
            for (int i = 0; i < arr.Length; i++)
                b[i] = arr[i];
            return ((long)arr.Length, null);
        }

        public static (BigInteger, object?) Int(object reader, BigInteger max)
        {
            // Simplified: use system crypto RNG
            var bytes = max.ToByteArray();
            BigInteger result;
            do
            {
                RandomNumberGenerator.Fill(bytes);
                bytes[bytes.Length - 1] &= 0x7F; // ensure positive
                result = new BigInteger(bytes);
            } while (result >= max || result < 0);
            return (result, null);
        }
    }
}
