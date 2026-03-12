using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Rsa
{
    [GoPackage("crypto/rsa")]
    public static class Package
    {
        // Error variables
        [GoVar] public static readonly object? ErrVerification = "crypto/rsa: verification error";
        [GoVar] public static readonly object? ErrDecryption = "crypto/rsa: decryption error";
        [GoVar] public static readonly object? ErrMessageTooLong = "crypto/rsa: message too long for RSA public key size";

        // rsa.GenerateKey(random io.Reader, bits int) (*PrivateKey, error)
        [GoFunc]
        [return: GoReturn("*rsa.PrivateKey", "error")]
        public static (GoPrivateKey?, object?) GenerateKey(object? random, [GoParam("int")] long bits) => (new GoPrivateKey(), null);

        // rsa.EncryptPKCS1v15(random io.Reader, pub *PublicKey, msg []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) EncryptPKCS1v15(object? random, [GoParam("*rsa.PublicKey")] GoPublicKey? pub, Slice<byte> msg)
            => (new Slice<byte>(), null);

        // rsa.DecryptPKCS1v15(random io.Reader, priv *PrivateKey, ciphertext []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) DecryptPKCS1v15(object? random, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, Slice<byte> ciphertext)
            => (new Slice<byte>(), null);

        // rsa.SignPKCS1v15(random io.Reader, priv *PrivateKey, hash crypto.Hash, hashed []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) SignPKCS1v15(object? random, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, [GoParam("crypto.Hash")] long hash, Slice<byte> hashed)
            => (new Slice<byte>(), null);

        // rsa.VerifyPKCS1v15(pub *PublicKey, hash crypto.Hash, hashed []byte, sig []byte) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? VerifyPKCS1v15([GoParam("*rsa.PublicKey")] GoPublicKey? pub, [GoParam("crypto.Hash")] long hash, Slice<byte> hashed, Slice<byte> sig)
            => null;

        // rsa.EncryptOAEP(hash hash.Hash, random io.Reader, pub *PublicKey, msg []byte, label []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) EncryptOAEP([GoParam("hash.Hash")] object? hash, object? random, [GoParam("*rsa.PublicKey")] GoPublicKey? pub, Slice<byte> msg, Slice<byte> label)
            => (new Slice<byte>(), null);

        // rsa.DecryptOAEP(hash hash.Hash, random io.Reader, priv *PrivateKey, ciphertext []byte, label []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) DecryptOAEP([GoParam("hash.Hash")] object? hash, object? random, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, Slice<byte> ciphertext, Slice<byte> label)
            => (new Slice<byte>(), null);

        // rsa.VerifyPSS(pub *PublicKey, hash crypto.Hash, digest []byte, sig []byte, opts *PSSOptions) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? VerifyPSS([GoParam("*rsa.PublicKey")] GoPublicKey? pub, [GoParam("crypto.Hash")] long hash, Slice<byte> digest, Slice<byte> sig, [GoParam("*rsa.PSSOptions")] object? opts)
            => null;

        // rsa.SignPSS(rand io.Reader, priv *PrivateKey, hash crypto.Hash, digest []byte, opts *PSSOptions) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) SignPSS(object? rand, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, [GoParam("crypto.Hash")] long hash, Slice<byte> digest, [GoParam("*rsa.PSSOptions")] object? opts)
            => (new Slice<byte>(), null);

        // PSS salt length constants
        [GoConst]
        public const long PSSSaltLengthAuto = 0;
        [GoConst]
        public const long PSSSaltLengthEqualsHash = -1;

        // PSSOptions struct
        [GoType("struct", Name = "PSSOptions", Package = "crypto/rsa")]
        public class GoPSSOptions
        {
            [GoField(Name = "SaltLength")] public long SaltLength;
            [GoField(Name = "Hash", Type = "crypto.Hash")] public long Hash;
        }

        // PKCS1v15DecryptOptions struct
        [GoType("struct", Name = "PKCS1v15DecryptOptions", Package = "crypto/rsa")]
        public class GoPKCS1v15DecryptOptions
        {
            [GoField(Name = "SessionKeyLen")] public long SessionKeyLen;
        }

        // OAEPOptions struct
        [GoType("struct", Name = "OAEPOptions", Package = "crypto/rsa")]
        public class GoOAEPOptions
        {
            [GoField(Name = "Hash", Type = "crypto.Hash")] public long Hash;
            [GoField(Name = "MGFHash", Type = "crypto.Hash")] public long MGFHash;
            [GoField(Name = "Label")] public Slice<byte> Label;
        }
    }
}
