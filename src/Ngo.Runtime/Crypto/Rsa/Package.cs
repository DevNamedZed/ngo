using System;
using System.Security.Cryptography;
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
        public static (GoPrivateKey?, object?) GenerateKey(object? random, [GoParam("int")] long bits)
        {
            try
            {
                using var rsa = RSA.Create((int)bits);
                var priv = GoPrivateKey.FromRSA(rsa);
                return (priv, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // rsa.EncryptPKCS1v15(random io.Reader, pub *PublicKey, msg []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) EncryptPKCS1v15(object? random, [GoParam("*rsa.PublicKey")] GoPublicKey? pub, Slice<byte> msg)
        {
            if (pub == null)
            {
                return (new Slice<byte>(), "crypto/rsa: nil public key");
            }
            try
            {
                using var rsa = pub.ToRSA();
                var plaintext = SliceToArray(msg);
                var encrypted = rsa.Encrypt(plaintext, RSAEncryptionPadding.Pkcs1);
                return (new Slice<byte>(encrypted), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        // rsa.DecryptPKCS1v15(random io.Reader, priv *PrivateKey, ciphertext []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) DecryptPKCS1v15(object? random, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, Slice<byte> ciphertext)
        {
            if (priv == null)
            {
                return (new Slice<byte>(), "crypto/rsa: nil private key");
            }
            try
            {
                using var rsa = priv.ToRSA();
                var ct = SliceToArray(ciphertext);
                var decrypted = rsa.Decrypt(ct, RSAEncryptionPadding.Pkcs1);
                return (new Slice<byte>(decrypted), null);
            }
            catch (CryptographicException)
            {
                return (new Slice<byte>(), ErrDecryption);
            }
        }

        // rsa.SignPKCS1v15(random io.Reader, priv *PrivateKey, hash crypto.Hash, hashed []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) SignPKCS1v15(object? random, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, [GoParam("crypto.Hash")] long hash, Slice<byte> hashed)
        {
            if (priv == null)
            {
                return (new Slice<byte>(), "crypto/rsa: nil private key");
            }
            try
            {
                using var rsa = priv.ToRSA();
                var hashName = CryptoHashToAlgorithmName(hash);
                var data = SliceToArray(hashed);
                var sig = rsa.SignHash(data, hashName, RSASignaturePadding.Pkcs1);
                return (new Slice<byte>(sig), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        // rsa.VerifyPKCS1v15(pub *PublicKey, hash crypto.Hash, hashed []byte, sig []byte) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? VerifyPKCS1v15([GoParam("*rsa.PublicKey")] GoPublicKey? pub, [GoParam("crypto.Hash")] long hash, Slice<byte> hashed, Slice<byte> sig)
        {
            if (pub == null)
            {
                return "crypto/rsa: nil public key";
            }
            try
            {
                using var rsa = pub.ToRSA();
                var hashName = CryptoHashToAlgorithmName(hash);
                var data = SliceToArray(hashed);
                var sigBytes = SliceToArray(sig);
                if (rsa.VerifyHash(data, sigBytes, hashName, RSASignaturePadding.Pkcs1))
                {
                    return null;
                }
                return ErrVerification;
            }
            catch
            {
                return ErrVerification;
            }
        }

        // rsa.EncryptOAEP(hash hash.Hash, random io.Reader, pub *PublicKey, msg []byte, label []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) EncryptOAEP([GoParam("hash.Hash")] object? hash, object? random, [GoParam("*rsa.PublicKey")] GoPublicKey? pub, Slice<byte> msg, Slice<byte> label)
        {
            if (pub == null)
            {
                return (new Slice<byte>(), "crypto/rsa: nil public key");
            }
            try
            {
                using var rsa = pub.ToRSA();
                var hashAlg = DetectHashAlgorithmFromHasher(hash);
                var padding = RSAEncryptionPadding.CreateOaep(hashAlg);
                var plaintext = SliceToArray(msg);
                var encrypted = rsa.Encrypt(plaintext, padding);
                return (new Slice<byte>(encrypted), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        // rsa.DecryptOAEP(hash hash.Hash, random io.Reader, priv *PrivateKey, ciphertext []byte, label []byte) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) DecryptOAEP([GoParam("hash.Hash")] object? hash, object? random, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, Slice<byte> ciphertext, Slice<byte> label)
        {
            if (priv == null)
            {
                return (new Slice<byte>(), "crypto/rsa: nil private key");
            }
            try
            {
                using var rsa = priv.ToRSA();
                var hashAlg = DetectHashAlgorithmFromHasher(hash);
                var padding = RSAEncryptionPadding.CreateOaep(hashAlg);
                var ct = SliceToArray(ciphertext);
                var decrypted = rsa.Decrypt(ct, padding);
                return (new Slice<byte>(decrypted), null);
            }
            catch (CryptographicException)
            {
                return (new Slice<byte>(), ErrDecryption);
            }
        }

        // rsa.SignPSS(rand io.Reader, priv *PrivateKey, hash crypto.Hash, digest []byte, opts *PSSOptions) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) SignPSS(object? rand, [GoParam("*rsa.PrivateKey")] GoPrivateKey? priv, [GoParam("crypto.Hash")] long hash, Slice<byte> digest, [GoParam("*rsa.PSSOptions")] object? opts)
        {
            if (priv == null)
            {
                return (new Slice<byte>(), "crypto/rsa: nil private key");
            }
            try
            {
                using var rsa = priv.ToRSA();
                var hashName = CryptoHashToAlgorithmName(hash);
                var data = SliceToArray(digest);
                var sig = rsa.SignHash(data, hashName, RSASignaturePadding.Pss);
                return (new Slice<byte>(sig), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        // rsa.VerifyPSS(pub *PublicKey, hash crypto.Hash, digest []byte, sig []byte, opts *PSSOptions) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? VerifyPSS([GoParam("*rsa.PublicKey")] GoPublicKey? pub, [GoParam("crypto.Hash")] long hash, Slice<byte> digest, Slice<byte> sig, [GoParam("*rsa.PSSOptions")] object? opts)
        {
            if (pub == null)
            {
                return "crypto/rsa: nil public key";
            }
            try
            {
                using var rsa = pub.ToRSA();
                var hashName = CryptoHashToAlgorithmName(hash);
                var data = SliceToArray(digest);
                var sigBytes = SliceToArray(sig);
                if (rsa.VerifyHash(data, sigBytes, hashName, RSASignaturePadding.Pss))
                {
                    return null;
                }
                return ErrVerification;
            }
            catch
            {
                return ErrVerification;
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? DecryptPKCS1v15SessionKey(object? rand, object? priv, Slice<byte> ciphertext, Slice<byte> key) => null;

        // PSS salt length constants
        [GoConst]
        public const long PSSSaltLengthAuto = 0;
        [GoConst]
        public const long PSSSaltLengthEqualsHash = -1;

        // PKCS1v15DecryptOptions struct
        [GoType("struct", Name = "PKCS1v15DecryptOptions", Package = "crypto/rsa")]
        public class GoPKCS1v15DecryptOptions
        {
            [GoField(Name = "SessionKeyLen")] public long SessionKeyLen;
        }

        internal static HashAlgorithmName CryptoHashToAlgorithmName(long hash)
        {
            // Maps crypto.Hash constants to .NET HashAlgorithmName
            // crypto.Hash constants: MD5=2, SHA1=3, SHA224=4, SHA256=5, SHA384=6, SHA512=7, SHA512_224=14, SHA512_256=15
            if (hash == 3)
            {
                return HashAlgorithmName.SHA1;
            }
            if (hash == 5 || hash == 4)
            {
                return HashAlgorithmName.SHA256;
            }
            if (hash == 6)
            {
                return HashAlgorithmName.SHA384;
            }
            if (hash == 7 || hash == 14 || hash == 15)
            {
                return HashAlgorithmName.SHA512;
            }
            if (hash == 2)
            {
                return HashAlgorithmName.MD5;
            }
            return HashAlgorithmName.SHA256;
        }

        private static HashAlgorithmName DetectHashAlgorithmFromHasher(object? hasher)
        {
            if (hasher == null)
            {
                return HashAlgorithmName.SHA256;
            }
            // Try to call Size() to detect hash algorithm
            var sizeMethod = hasher.GetType().GetMethod("Size");
            if (sizeMethod != null)
            {
                var size = sizeMethod.Invoke(hasher, null);
                if (size is long s)
                {
                    if (s == 20)
                    {
                        return HashAlgorithmName.SHA1;
                    }
                    if (s == 32)
                    {
                        return HashAlgorithmName.SHA256;
                    }
                    if (s == 48)
                    {
                        return HashAlgorithmName.SHA384;
                    }
                    if (s == 64)
                    {
                        return HashAlgorithmName.SHA512;
                    }
                    if (s == 16)
                    {
                        return HashAlgorithmName.MD5;
                    }
                }
            }
            return HashAlgorithmName.SHA256;
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
}
