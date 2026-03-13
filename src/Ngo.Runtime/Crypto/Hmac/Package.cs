using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Hmac
{
    [GoPackage("crypto/hmac")]
    public static class Package
    {
        // hmac.New(h func() hash.Hash, key []byte) hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static GoHmacHash New([GoParam("func() hash.Hash")] Func<object> h, Slice<byte> key)
        {
            var keyArr = new byte[key.Len];
            for (int i = 0; i < key.Len; i++)
            {
                keyArr[i] = key[i];
            }

            // Detect which hash algorithm by calling the factory and checking Size()
            var sample = h();
            HashAlgorithmName alg = HashAlgorithmName.SHA256; // default
            if (sample != null)
            {
                long size = 0;
                var sizeMethod = sample.GetType().GetMethod("Size");
                if (sizeMethod != null)
                {
                    var result = sizeMethod.Invoke(sample, null);
                    if (result is long l)
                    {
                        size = l;
                    }
                    else if (result is int i)
                    {
                        size = i;
                    }
                }

                if (size == 20)
                {
                    alg = HashAlgorithmName.SHA1;
                }
                else if (size == 32)
                {
                    alg = HashAlgorithmName.SHA256;
                }
                else if (size == 48)
                {
                    alg = HashAlgorithmName.SHA384;
                }
                else if (size == 64)
                {
                    alg = HashAlgorithmName.SHA512;
                }
                else if (size == 16)
                {
                    alg = HashAlgorithmName.MD5;
                }
            }

            return new GoHmacHash(alg, keyArr);
        }

        // hmac.Equal(mac1, mac2 []byte) bool
        [GoFunc]
        public static bool Equal(Slice<byte> mac1, Slice<byte> mac2)
        {
            if (mac1.Len != mac2.Len)
            {
                return false;
            }
            var a = new byte[mac1.Len];
            var b = new byte[mac2.Len];
            for (int i = 0; i < mac1.Len; i++)
            {
                a[i] = mac1[i];
            }
            for (int i = 0; i < mac2.Len; i++)
            {
                b[i] = mac2[i];
            }
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }

    [GoType("struct", Name = "HmacHash", Package = "crypto/hmac")]
    public class GoHmacHash
    {
        private IncrementalHash _hash;
        private readonly HashAlgorithmName _algorithm;
        private readonly byte[] _key;

        public GoHmacHash(HashAlgorithmName algorithm, byte[] key)
        {
            _algorithm = algorithm;
            _key = key;
            _hash = IncrementalHash.CreateHMAC(algorithm, key);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p)
        {
            var arr = new byte[p.Len];
            for (int i = 0; i < p.Len; i++)
            {
                arr[i] = p[i];
            }
            _hash.AppendData(arr);
            return (arr.Length, null);
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Sum(Slice<byte> b)
        {
            var mac = _hash.GetCurrentHash();
            var result = new byte[b.Len + mac.Length];
            for (int i = 0; i < b.Len; i++)
            {
                result[i] = b[i];
            }
            Array.Copy(mac, 0, result, b.Len, mac.Length);
            return new Slice<byte>(result);
        }

        [GoMethod]
        public void Reset()
        {
            _hash.Dispose();
            _hash = IncrementalHash.CreateHMAC(_algorithm, _key);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Size()
        {
            if (_algorithm == HashAlgorithmName.SHA1) { return 20; }
            if (_algorithm == HashAlgorithmName.SHA256) { return 32; }
            if (_algorithm == HashAlgorithmName.SHA384) { return 48; }
            if (_algorithm == HashAlgorithmName.SHA512) { return 64; }
            if (_algorithm == HashAlgorithmName.MD5) { return 16; }
            return 32;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long BlockSize()
        {
            if (_algorithm == HashAlgorithmName.SHA1) { return 64; }
            if (_algorithm == HashAlgorithmName.SHA256) { return 64; }
            if (_algorithm == HashAlgorithmName.SHA384) { return 128; }
            if (_algorithm == HashAlgorithmName.SHA512) { return 128; }
            if (_algorithm == HashAlgorithmName.MD5) { return 64; }
            return 64;
        }
    }
}
