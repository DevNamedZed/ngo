using System;
using System.Security.Cryptography;
using Ngo.Runtime.Crypto.Cipher;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Des
{
    [GoPackage("crypto/des")]
    public static class Package
    {
        // des.BlockSize
        [GoConst(Type = "int")]
        public const long BlockSize = 8;

        // des.NewCipher(key []byte) (cipher.Block, error)
        [GoFunc]
        [return: GoReturn("cipher.Block", "error")]
        public static (object?, object?) NewCipher(Slice<byte> key)
        {
            if (key.Len != 8)
            {
                return (null, (object)("crypto/des: invalid key size " + key.Len));
            }
            var keyBytes = SliceToArray(key);
            return (new DesBlock(keyBytes, false), null);
        }

        // des.NewTripleDESCipher(key []byte) (cipher.Block, error)
        [GoFunc]
        [return: GoReturn("cipher.Block", "error")]
        public static (object?, object?) NewTripleDESCipher(Slice<byte> key)
        {
            if (key.Len != 24)
            {
                return (null, (object)("crypto/des: invalid key size " + key.Len));
            }
            var keyBytes = SliceToArray(key);
            return (new DesBlock(keyBytes, true), null);
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

    internal class DesBlock : Cipher.Package.IBlock
    {
        private readonly byte[] _key;
        private readonly bool _tripleDes;

        public DesBlock(byte[] key, bool tripleDes)
        {
            _key = key;
            _tripleDes = tripleDes;
        }

        public long BlockSize()
        {
            return Package.BlockSize;
        }

        public void Encrypt(Slice<byte> dst, Slice<byte> src)
        {
            var input = Package.SliceToArray(src);
            byte[] output;
            if (_tripleDes)
            {
                using var des = TripleDES.Create();
                des.Key = _key;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;
                output = new byte[8];
                des.EncryptEcb(input.AsSpan(0, 8), output, PaddingMode.None);
            }
            else
            {
                using var des = DES.Create();
                des.Key = _key;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;
                output = new byte[8];
                des.EncryptEcb(input.AsSpan(0, 8), output, PaddingMode.None);
            }
            for (int i = 0; i < 8 && i < dst.Len; i++)
            {
                dst[i] = output[i];
            }
        }

        public void Decrypt(Slice<byte> dst, Slice<byte> src)
        {
            var input = Package.SliceToArray(src);
            byte[] output;
            if (_tripleDes)
            {
                using var des = TripleDES.Create();
                des.Key = _key;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;
                output = new byte[8];
                des.DecryptEcb(input.AsSpan(0, 8), output, PaddingMode.None);
            }
            else
            {
                using var des = DES.Create();
                des.Key = _key;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;
                output = new byte[8];
                des.DecryptEcb(input.AsSpan(0, 8), output, PaddingMode.None);
            }
            for (int i = 0; i < 8 && i < dst.Len; i++)
            {
                dst[i] = output[i];
            }
        }
    }
}
