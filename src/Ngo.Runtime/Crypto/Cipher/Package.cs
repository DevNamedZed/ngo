using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Crypto.Cipher
{
    [GoPackage("crypto/cipher")]
    public static class Package
    {
        // cipher.Block interface
        [GoType("interface", Name = "Block", Package = "crypto/cipher")]
        public interface IBlock
        {
            [GoMethod]
            [return: GoReturn("int")]
            long BlockSize();

            [GoMethod]
            void Encrypt(Slice<byte> dst, Slice<byte> src);

            [GoMethod]
            void Decrypt(Slice<byte> dst, Slice<byte> src);
        }

        // cipher.BlockMode interface
        [GoType("interface", Name = "BlockMode", Package = "crypto/cipher")]
        public interface IBlockMode
        {
            [GoMethod]
            [return: GoReturn("int")]
            long BlockSize();

            [GoMethod]
            void CryptBlocks(Slice<byte> dst, Slice<byte> src);
        }

        // cipher.Stream interface
        [GoType("interface", Name = "Stream", Package = "crypto/cipher")]
        public interface IStream
        {
            [GoMethod]
            void XORKeyStream(Slice<byte> dst, Slice<byte> src);
        }

        // cipher.AEAD interface
        [GoType("interface", Name = "AEAD", Package = "crypto/cipher")]
        public interface IAEAD
        {
            [GoMethod]
            [return: GoReturn("int")]
            long NonceSize();

            [GoMethod]
            [return: GoReturn("int")]
            long Overhead();

            [GoMethod]
            [return: GoReturn("[]byte")]
            Slice<byte> Seal(Slice<byte> dst, Slice<byte> nonce, Slice<byte> plaintext, Slice<byte> additionalData);

            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) Open(Slice<byte> dst, Slice<byte> nonce, Slice<byte> ciphertext, Slice<byte> additionalData);
        }

        // cipher.NewGCM(cipher Block) (AEAD, error)
        [GoFunc]
        [return: GoReturn("cipher.AEAD", "error")]
        public static (object?, object?) NewGCM([GoParam("cipher.Block")] object? cipher)
        {
            return NewGCMWithNonceSize(cipher, 12);
        }

        // cipher.NewGCMWithNonceSize(cipher Block, size int) (AEAD, error)
        [GoFunc]
        [return: GoReturn("cipher.AEAD", "error")]
        public static (object?, object?) NewGCMWithNonceSize([GoParam("cipher.Block")] object? cipher, [GoParam("int")] long size)
        {
            if (cipher is not IBlock block)
            {
                return (null, "cipher: NewGCM requires cipher.Block");
            }
            if (block.BlockSize() != 16)
            {
                return (null, "cipher: NewGCM requires 128-bit block cipher");
            }
            return (new GcmAead(block, (int)size), null);
        }

        // cipher.NewCBCEncrypter(b Block, iv []byte) BlockMode
        [GoFunc]
        [return: GoReturn("cipher.BlockMode")]
        public static object? NewCBCEncrypter([GoParam("cipher.Block")] object? b, Slice<byte> iv)
        {
            if (b is not IBlock block)
            {
                return null;
            }
            return new CbcEncrypter(block, iv);
        }

        // cipher.NewCBCDecrypter(b Block, iv []byte) BlockMode
        [GoFunc]
        [return: GoReturn("cipher.BlockMode")]
        public static object? NewCBCDecrypter([GoParam("cipher.Block")] object? b, Slice<byte> iv)
        {
            if (b is not IBlock block)
            {
                return null;
            }
            return new CbcDecrypter(block, iv);
        }

        // cipher.NewCFBEncrypter(block Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewCFBEncrypter([GoParam("cipher.Block")] object? block, Slice<byte> iv)
        {
            if (block is not IBlock b)
            {
                return null;
            }
            return new CfbStream(b, iv, true);
        }

        // cipher.NewCFBDecrypter(block Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewCFBDecrypter([GoParam("cipher.Block")] object? block, Slice<byte> iv)
        {
            if (block is not IBlock b)
            {
                return null;
            }
            return new CfbStream(b, iv, false);
        }

        // cipher.NewOFB(b Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewOFB([GoParam("cipher.Block")] object? b, Slice<byte> iv)
        {
            if (b is not IBlock block)
            {
                return null;
            }
            return new OfbStream(block, iv);
        }

        // cipher.NewCTR(block Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewCTR([GoParam("cipher.Block")] object? block, Slice<byte> iv)
        {
            if (block is not IBlock b)
            {
                return null;
            }
            return new CtrStream(b, iv);
        }

        // cipher.StreamReader struct
        [GoType("struct", Name = "StreamReader", Package = "crypto/cipher")]
        public class GoStreamReader
        {
            [GoField(Name = "S", Type = "cipher.Stream")] public object? S;
            [GoField(Name = "R", Type = "io.Reader")] public object? R;

            [GoMethod]
            [return: GoReturn("int", "error")]
            public (long, object?) Read(Slice<byte> dst)
            {
                if (R is not IGoReader reader || S is not IStream stream)
                {
                    return (0, "cipher: StreamReader not initialized");
                }
                var result = reader.Read(dst);
                if (result.Item1 > 0)
                {
                    var sub = dst.Reslice(0, (int)result.Item1);
                    stream.XORKeyStream(sub, sub);
                }
                return result;
            }
        }

        // cipher.StreamWriter struct
        [GoType("struct", Name = "StreamWriter", Package = "crypto/cipher")]
        public class GoStreamWriter
        {
            [GoField(Name = "S", Type = "cipher.Stream")] public object? S;
            [GoField(Name = "W", Type = "io.Writer")] public object? W;
            [GoField(Name = "Err")] public object? Err;

            [GoMethod]
            [return: GoReturn("int", "error")]
            public (long, object?) Write(Slice<byte> src)
            {
                if (W is not IGoWriter writer || S is not IStream stream)
                {
                    return (0, "cipher: StreamWriter not initialized");
                }
                var encrypted = new byte[src.Len];
                var dst = new Slice<byte>(encrypted);
                stream.XORKeyStream(dst, src);
                var result = writer.Write(dst);
                if (!string.IsNullOrEmpty(result.Item2))
                {
                    Err = result.Item2;
                }
                return (result.Item1, Err);
            }

            [GoMethod]
            [return: GoReturn("error")]
            public object? Close()
            {
                if (W is IGoCloser closer)
                {
                    return closer.Close();
                }
                return null;
            }
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

        internal static byte[] SliceToArray(Slice<byte> s, int len)
        {
            var arr = new byte[len];
            for (int i = 0; i < len && i < s.Len; i++)
            {
                arr[i] = s[i];
            }
            return arr;
        }
    }

    // CBC encrypter
    internal class CbcEncrypter : Package.IBlockMode
    {
        private readonly Package.IBlock _block;
        private readonly byte[] _iv;
        private readonly int _blockSize;

        public CbcEncrypter(Package.IBlock block, Slice<byte> iv)
        {
            _block = block;
            _blockSize = (int)block.BlockSize();
            _iv = Package.SliceToArray(iv, _blockSize);
        }

        public long BlockSize()
        {
            return _blockSize;
        }

        public void CryptBlocks(Slice<byte> dst, Slice<byte> src)
        {
            if (src.Len % _blockSize != 0)
            {
                throw new GoPanicException("crypto/cipher: input not full blocks");
            }
            var prev = new byte[_blockSize];
            Array.Copy(_iv, prev, _blockSize);
            var blockIn = new byte[_blockSize];
            var blockOut = new byte[_blockSize];

            for (int offset = 0; offset < src.Len; offset += _blockSize)
            {
                // XOR plaintext with previous ciphertext
                for (int i = 0; i < _blockSize; i++)
                {
                    blockIn[i] = (byte)(src[offset + i] ^ prev[i]);
                }
                var inSlice = new Slice<byte>(blockIn);
                var outSlice = new Slice<byte>(blockOut);
                _block.Encrypt(outSlice, inSlice);
                for (int i = 0; i < _blockSize; i++)
                {
                    dst[offset + i] = blockOut[i];
                    prev[i] = blockOut[i];
                }
            }
            Array.Copy(prev, _iv, _blockSize);
        }
    }

    // CBC decrypter
    internal class CbcDecrypter : Package.IBlockMode
    {
        private readonly Package.IBlock _block;
        private readonly byte[] _iv;
        private readonly int _blockSize;

        public CbcDecrypter(Package.IBlock block, Slice<byte> iv)
        {
            _block = block;
            _blockSize = (int)block.BlockSize();
            _iv = Package.SliceToArray(iv, _blockSize);
        }

        public long BlockSize()
        {
            return _blockSize;
        }

        public void CryptBlocks(Slice<byte> dst, Slice<byte> src)
        {
            if (src.Len % _blockSize != 0)
            {
                throw new GoPanicException("crypto/cipher: input not full blocks");
            }
            var prev = new byte[_blockSize];
            Array.Copy(_iv, prev, _blockSize);
            var blockIn = new byte[_blockSize];
            var blockOut = new byte[_blockSize];

            for (int offset = 0; offset < src.Len; offset += _blockSize)
            {
                for (int i = 0; i < _blockSize; i++)
                {
                    blockIn[i] = src[offset + i];
                }
                var inSlice = new Slice<byte>(blockIn);
                var outSlice = new Slice<byte>(blockOut);
                _block.Decrypt(outSlice, inSlice);
                for (int i = 0; i < _blockSize; i++)
                {
                    dst[offset + i] = (byte)(blockOut[i] ^ prev[i]);
                    prev[i] = blockIn[i];
                }
            }
            Array.Copy(prev, _iv, _blockSize);
        }
    }

    // CFB stream (encrypt or decrypt)
    internal class CfbStream : Package.IStream
    {
        private readonly Package.IBlock _block;
        private readonly byte[] _shift;
        private readonly int _blockSize;
        private readonly bool _encrypt;

        public CfbStream(Package.IBlock block, Slice<byte> iv, bool encrypt)
        {
            _block = block;
            _blockSize = (int)block.BlockSize();
            _shift = Package.SliceToArray(iv, _blockSize);
            _encrypt = encrypt;
        }

        public void XORKeyStream(Slice<byte> dst, Slice<byte> src)
        {
            var encrypted = new byte[_blockSize];
            for (int i = 0; i < src.Len; i++)
            {
                if (i % _blockSize == 0)
                {
                    var shiftSlice = new Slice<byte>(_shift);
                    var encSlice = new Slice<byte>(encrypted);
                    _block.Encrypt(encSlice, shiftSlice);
                }
                int idx = i % _blockSize;
                if (_encrypt)
                {
                    byte c = (byte)(src[i] ^ encrypted[idx]);
                    dst[i] = c;
                    _shift[idx] = c;
                }
                else
                {
                    byte c = src[i];
                    dst[i] = (byte)(c ^ encrypted[idx]);
                    _shift[idx] = c;
                }
                if (idx == _blockSize - 1)
                {
                    // shift register already updated byte-by-byte
                }
            }
        }
    }

    // OFB stream
    internal class OfbStream : Package.IStream
    {
        private readonly Package.IBlock _block;
        private readonly byte[] _feedback;
        private readonly int _blockSize;

        public OfbStream(Package.IBlock block, Slice<byte> iv)
        {
            _block = block;
            _blockSize = (int)block.BlockSize();
            _feedback = Package.SliceToArray(iv, _blockSize);
        }

        public void XORKeyStream(Slice<byte> dst, Slice<byte> src)
        {
            var encrypted = new byte[_blockSize];
            for (int i = 0; i < src.Len; i++)
            {
                if (i % _blockSize == 0)
                {
                    var fbSlice = new Slice<byte>(_feedback);
                    var encSlice = new Slice<byte>(encrypted);
                    _block.Encrypt(encSlice, fbSlice);
                    Array.Copy(encrypted, _feedback, _blockSize);
                }
                dst[i] = (byte)(src[i] ^ encrypted[i % _blockSize]);
            }
        }
    }

    // CTR stream
    internal class CtrStream : Package.IStream
    {
        private readonly Package.IBlock _block;
        private readonly byte[] _counter;
        private readonly int _blockSize;
        private readonly byte[] _keystream;
        private int _keystreamPos;

        public CtrStream(Package.IBlock block, Slice<byte> iv)
        {
            _block = block;
            _blockSize = (int)block.BlockSize();
            _counter = Package.SliceToArray(iv, _blockSize);
            _keystream = new byte[_blockSize];
            _keystreamPos = _blockSize; // force generation on first use
        }

        public void XORKeyStream(Slice<byte> dst, Slice<byte> src)
        {
            for (int i = 0; i < src.Len; i++)
            {
                if (_keystreamPos >= _blockSize)
                {
                    var ctrSlice = new Slice<byte>(_counter);
                    var ksSlice = new Slice<byte>(_keystream);
                    _block.Encrypt(ksSlice, ctrSlice);
                    _keystreamPos = 0;
                    IncrementCounter();
                }
                dst[i] = (byte)(src[i] ^ _keystream[_keystreamPos]);
                _keystreamPos++;
            }
        }

        private void IncrementCounter()
        {
            for (int i = _blockSize - 1; i >= 0; i--)
            {
                _counter[i]++;
                if (_counter[i] != 0)
                {
                    break;
                }
            }
        }
    }

    // GCM AEAD using .NET's AesGcm
    internal class GcmAead : Package.IAEAD
    {
        private readonly Package.IBlock _block;
        private readonly int _nonceSize;
        private const int TagSize = 16;

        public GcmAead(Package.IBlock block, int nonceSize)
        {
            _block = block;
            _nonceSize = nonceSize;
        }

        public long NonceSize()
        {
            return _nonceSize;
        }

        public long Overhead()
        {
            return TagSize;
        }

        public Slice<byte> Seal(Slice<byte> dst, Slice<byte> nonce, Slice<byte> plaintext, Slice<byte> additionalData)
        {
            // Extract key from block if it's our AesBlock
            byte[]? key = GetKeyFromBlock();
            if (key == null)
            {
                throw new GoPanicException("cipher: GCM requires AES block cipher");
            }

            var nonceBytes = Package.SliceToArray(nonce);
            var plaintextBytes = Package.SliceToArray(plaintext);
            var aadBytes = (additionalData.IsNil || additionalData.Len == 0) ? Array.Empty<byte>() : Package.SliceToArray(additionalData);

            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            using var gcm = new AesGcm(key, TagSize);
            gcm.Encrypt(nonceBytes, plaintextBytes, ciphertext, tag, aadBytes);

            // Build output: dst prefix + ciphertext + tag
            int dstLen = dst.IsNil ? 0 : dst.Len;
            var result = new byte[dstLen + ciphertext.Length + TagSize];
            for (int i = 0; i < dstLen; i++)
            {
                result[i] = dst[i];
            }
            Array.Copy(ciphertext, 0, result, dstLen, ciphertext.Length);
            Array.Copy(tag, 0, result, dstLen + ciphertext.Length, TagSize);
            return new Slice<byte>(result);
        }

        public (Slice<byte>, object?) Open(Slice<byte> dst, Slice<byte> nonce, Slice<byte> ciphertext, Slice<byte> additionalData)
        {
            byte[]? key = GetKeyFromBlock();
            if (key == null)
            {
                return (new Slice<byte>(), "cipher: GCM requires AES block cipher");
            }

            if (ciphertext.Len < TagSize)
            {
                return (new Slice<byte>(), "cipher: message authentication failed");
            }

            var nonceBytes = Package.SliceToArray(nonce);
            var aadBytes = (additionalData.IsNil || additionalData.Len == 0) ? Array.Empty<byte>() : Package.SliceToArray(additionalData);

            int ctLen = ciphertext.Len - TagSize;
            var ctBytes = new byte[ctLen];
            var tagBytes = new byte[TagSize];
            for (int i = 0; i < ctLen; i++)
            {
                ctBytes[i] = ciphertext[i];
            }
            for (int i = 0; i < TagSize; i++)
            {
                tagBytes[i] = ciphertext[ctLen + i];
            }

            var plaintext = new byte[ctLen];
            try
            {
                using var gcm = new AesGcm(key, TagSize);
                gcm.Decrypt(nonceBytes, ctBytes, tagBytes, plaintext, aadBytes);
            }
            catch
            {
                return (new Slice<byte>(), "cipher: message authentication failed");
            }

            int dstLen = dst.IsNil ? 0 : dst.Len;
            var result = new byte[dstLen + plaintext.Length];
            for (int i = 0; i < dstLen; i++)
            {
                result[i] = dst[i];
            }
            Array.Copy(plaintext, 0, result, dstLen, plaintext.Length);
            return (new Slice<byte>(result), null);
        }

        private byte[]? GetKeyFromBlock()
        {
            // Use reflection to get key from our AesBlock
            var keyProp = _block.GetType().GetProperty("Key");
            if (keyProp != null)
            {
                return keyProp.GetValue(_block) as byte[];
            }
            // Try field access
            var keyField = _block.GetType().GetField("_key", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (keyField != null)
            {
                return keyField.GetValue(_block) as byte[];
            }
            return null;
        }
    }
}
