using Ngo.Runtime.Discovery;

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
        public static (object?, object?) NewGCM([GoParam("cipher.Block")] object? cipher) => (null, null);

        // cipher.NewGCMWithNonceSize(cipher Block, size int) (AEAD, error)
        [GoFunc]
        [return: GoReturn("cipher.AEAD", "error")]
        public static (object?, object?) NewGCMWithNonceSize([GoParam("cipher.Block")] object? cipher, [GoParam("int")] long size) => (null, null);

        // cipher.NewCBCEncrypter(b Block, iv []byte) BlockMode
        [GoFunc]
        [return: GoReturn("cipher.BlockMode")]
        public static object? NewCBCEncrypter([GoParam("cipher.Block")] object? b, Slice<byte> iv) => null;

        // cipher.NewCBCDecrypter(b Block, iv []byte) BlockMode
        [GoFunc]
        [return: GoReturn("cipher.BlockMode")]
        public static object? NewCBCDecrypter([GoParam("cipher.Block")] object? b, Slice<byte> iv) => null;

        // cipher.NewCFBEncrypter(block Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewCFBEncrypter([GoParam("cipher.Block")] object? block, Slice<byte> iv) => null;

        // cipher.NewCFBDecrypter(block Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewCFBDecrypter([GoParam("cipher.Block")] object? block, Slice<byte> iv) => null;

        // cipher.NewOFB(b Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewOFB([GoParam("cipher.Block")] object? b, Slice<byte> iv) => null;

        // cipher.NewCTR(block Block, iv []byte) Stream
        [GoFunc]
        [return: GoReturn("cipher.Stream")]
        public static object? NewCTR([GoParam("cipher.Block")] object? block, Slice<byte> iv) => null;

        // cipher.StreamReader struct
        [GoType("struct", Name = "StreamReader", Package = "crypto/cipher")]
        public class GoStreamReader
        {
            [GoField(Name = "S", Type = "cipher.Stream")] public object? S;
            [GoField(Name = "R", Type = "io.Reader")] public object? R;

            [GoMethod]
            [return: GoReturn("int", "error")]
            public (long, object?) Read(Slice<byte> dst) => (0, null);
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
            public (long, object?) Write(Slice<byte> src) => (0, null);

            [GoMethod]
            [return: GoReturn("error")]
            public object? Close() => null;
        }
    }
}
