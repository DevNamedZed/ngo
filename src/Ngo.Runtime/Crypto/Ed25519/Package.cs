using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Ed25519
{
    [GoPackage("crypto/ed25519")]
    public static class Package
    {
        [GoConst]
        public const long PublicKeySize = 32;

        [GoConst]
        public const long PrivateKeySize = 64;

        [GoConst]
        public const long SignatureSize = 64;

        [GoConst]
        public const long SeedSize = 32;

        [GoType("named", Name = "PublicKey", Package = "crypto/ed25519", Underlying = "[]byte")]
        public struct GoPublicKeyType { }

        [GoType("named", Name = "PrivateKey", Package = "crypto/ed25519", Underlying = "[]byte")]
        public struct GoPrivateKeyType { }

        // ed25519.GenerateKey(rand io.Reader) (PublicKey, PrivateKey, error)
        [GoFunc]
        [return: GoReturn("ed25519.PublicKey", "ed25519.PrivateKey", "error")]
        public static (Slice<byte>, Slice<byte>, string) GenerateKey(object? rand)
        {
            throw new PlatformNotSupportedException(
                "crypto/ed25519: Ed25519 is not available in this .NET runtime — requires EdDSA API");
        }

        // ed25519.Sign(privateKey PrivateKey, message []byte) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> Sign([GoParam("ed25519.PrivateKey")] Slice<byte> privateKey, Slice<byte> message)
        {
            throw new PlatformNotSupportedException(
                "crypto/ed25519: Ed25519 is not available in this .NET runtime — requires EdDSA API");
        }

        // ed25519.Verify(publicKey PublicKey, message, sig []byte) bool
        [GoFunc]
        public static bool Verify([GoParam("ed25519.PublicKey")] Slice<byte> publicKey, Slice<byte> message, Slice<byte> sig)
        {
            throw new PlatformNotSupportedException(
                "crypto/ed25519: Ed25519 is not available in this .NET runtime — requires EdDSA API");
        }

        // ed25519.NewKeyFromSeed(seed []byte) PrivateKey
        [GoFunc]
        [return: GoReturn("ed25519.PrivateKey")]
        public static Slice<byte> NewKeyFromSeed(Slice<byte> seed)
        {
            throw new PlatformNotSupportedException(
                "crypto/ed25519: Ed25519 is not available in this .NET runtime — requires EdDSA API");
        }
    }
}
