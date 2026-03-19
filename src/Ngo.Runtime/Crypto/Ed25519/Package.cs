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
        public struct GoPublicKeyType
        {
            // func (pub PublicKey) Equal(x crypto.PublicKey) bool
            [GoMethod]
            public bool Equal([GoParam("crypto.PublicKey")] object? x) => false;
        }

        [GoType("named", Name = "PrivateKey", Package = "crypto/ed25519", Underlying = "[]byte")]
        public struct GoPrivateKeyType
        {
            // func (priv PrivateKey) Public() crypto.PublicKey
            [GoMethod]
            [return: GoReturn("crypto.PublicKey")]
            public object? Public() => null;

            // func (priv PrivateKey) Seed() []byte
            [GoMethod]
            [return: GoReturn("[]byte")]
            public Slice<byte> Seed() => default;

            // func (priv PrivateKey) Sign(rand io.Reader, digest []byte, opts crypto.SignerOpts) ([]byte, error)
            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            public (Slice<byte>, object?) Sign(object? rand, Slice<byte> digest, [GoParam("crypto.SignerOpts")] object? opts)
                => (default, "crypto/ed25519: not supported");

            // func (priv PrivateKey) Equal(x crypto.PrivateKey) bool
            [GoMethod]
            public bool Equal([GoParam("crypto.PrivateKey")] object? x) => false;
        }

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
