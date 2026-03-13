using System;
using System.Security.Cryptography;
using Ngo.Runtime.Crypto.Cipher;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Aes
{
    [GoPackage("crypto/aes")]
    public static class Package
    {
        // aes.BlockSize
        [GoConst(Type = "int")]
        public const long BlockSize = 16;

        // aes.NewCipher(key []byte) (cipher.Block, error)
        [GoFunc]
        [return: GoReturn("cipher.Block", "error")]
        public static (object?, object?) NewCipher(Slice<byte> key)
        {
            int keyLen = key.Len;
            if (keyLen != 16 && keyLen != 24 && keyLen != 32)
            {
                return (null, "crypto/aes: invalid key size " + keyLen);
            }
            var keyBytes = SliceToArray(key);
            return (new AesBlock(keyBytes), null);
        }

        internal static byte[] SliceToArray(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
            {
                arr[i] = s[i];
            }
            return arr;
        }
    }

    internal class AesBlock : Cipher.Package.IBlock
    {
        private readonly byte[] _key;

        public AesBlock(byte[] key)
        {
            _key = key;
        }

        public long BlockSize()
        {
            return Package.BlockSize;
        }

        public void Encrypt(Slice<byte> dst, Slice<byte> src)
        {
            var input = Package.SliceToArray(src);
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            var output = new byte[16];
            aes.EncryptEcb(input.AsSpan(0, 16), output, PaddingMode.None);
            for (int i = 0; i < 16 && i < dst.Len; i++)
            {
                dst[i] = output[i];
            }
        }

        public void Decrypt(Slice<byte> dst, Slice<byte> src)
        {
            var input = Package.SliceToArray(src);
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            var output = new byte[16];
            aes.DecryptEcb(input.AsSpan(0, 16), output, PaddingMode.None);
            for (int i = 0; i < 16 && i < dst.Len; i++)
            {
                dst[i] = output[i];
            }
        }

        internal byte[] Key => _key;
    }
}
