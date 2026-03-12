using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Rsa
{
    // rsa.PrivateKey struct
    [GoType("struct", Name = "PrivateKey", Package = "crypto/rsa")]
    public class GoPrivateKey
    {
        [GoField(Embedded = true)]
        public GoPublicKey PublicKey = new GoPublicKey();

        [GoField(Name = "D")] public object? D; // *big.Int
        [GoField(Name = "Primes")] public Slice<object?> Primes; // []*big.Int

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
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) Decrypt(object? rand, Slice<byte> msg, [GoParam("crypto.DecrypterOpts")] object? opts)
            => (new Slice<byte>(), null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Validate() => null;

        [GoMethod]
        public void Precompute() { }

        [GoField(Name = "Precomputed")]
        public GoPrecomputedValues Precomputed = new GoPrecomputedValues();
    }

    [GoType("struct", Name = "PrecomputedValues", Package = "crypto/rsa")]
    public class GoPrecomputedValues
    {
        [GoField(Name = "Dp", Type = "*big.Int")] public object? Dp;
        [GoField(Name = "Dq", Type = "*big.Int")] public object? Dq;
        [GoField(Name = "Qinv", Type = "*big.Int")] public object? Qinv;
        [GoField(Name = "CRTValues")] public Slice<GoCRTValue> CRTValues;
    }

    [GoType("struct", Name = "CRTValue", Package = "crypto/rsa")]
    public class GoCRTValue
    {
        [GoField(Name = "Exp", Type = "*big.Int")] public object? Exp;
        [GoField(Name = "Coeff", Type = "*big.Int")] public object? Coeff;
        [GoField(Name = "R", Type = "*big.Int")] public object? R;
    }
}
