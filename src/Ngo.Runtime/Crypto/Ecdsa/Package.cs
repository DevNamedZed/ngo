using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Ecdsa
{
    [GoPackage("crypto/ecdsa")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*PrivateKey", "error")]
        public static (object?, object?) GenerateKey(object? c, object? rand) => (new GoPrivateKey(), null);

        [GoFunc]
        public static bool VerifyASN1([GoParam("*PublicKey")] object? pub, Slice<byte> hash, Slice<byte> sig) => false;

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) SignASN1(object? rand, [GoParam("*PrivateKey")] object? priv, Slice<byte> hash) => (new Slice<byte>(), null);
    }

    [GoType("struct", Name = "PublicKey", Package = "crypto/ecdsa")]
    public class GoPublicKey
    {
        [GoField(Name = "Curve", Embedded = true, Type = "elliptic.Curve")]
        public object? Curve;

        [GoField(Name = "X", Type = "*big.Int")] public object? X;
        [GoField(Name = "Y", Type = "*big.Int")] public object? Y;

        [GoMethod]
        public bool Equal([GoParam("crypto.PublicKey")] object? x) => false;

        [GoMethod]
        [return: GoReturn("*ecdh.PublicKey", "error")]
        public (object?, object?) ECDH() => (null, null);
    }

    [GoType("struct", Name = "PrivateKey", Package = "crypto/ecdsa")]
    public class GoPrivateKey
    {
        [GoField(Embedded = true)]
        public GoPublicKey PublicKey = new GoPublicKey();

        [GoField(Name = "D", Type = "*big.Int")] public object? D;

        [GoMethod]
        [return: GoReturn("crypto.PublicKey")]
        public object? Public() => PublicKey;

        [GoMethod]
        public bool Equal([GoParam("crypto.PrivateKey")] object? x) => false;

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) Sign(object? rand, Slice<byte> digest, [GoParam("crypto.SignerOpts")] object? opts)
            => (new Slice<byte>(), null);

        [GoMethod]
        [return: GoReturn("*ecdh.PrivateKey", "error")]
        public (object?, object?) ECDH() => (null, null);
    }
}
