using System.Security.Cryptography;
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

        // Internal storage for full RSA parameters
        internal RSAParameters? _rsaParams;

        [GoMethod]
        [return: GoReturn("crypto.PublicKey")]
        public object? Public() => PublicKey;

        [GoMethod]
        public bool Equal([GoParam("crypto.PrivateKey")] object? x)
        {
            if (x is GoPrivateKey other)
            {
                return PublicKey.Equal(other.PublicKey);
            }
            return false;
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) Sign(object? rand, Slice<byte> digest, [GoParam("crypto.SignerOpts")] object? opts)
        {
            long hash = 5; // default
            if (opts != null)
            {
                var hashFunc = opts.GetType().GetMethod("HashFunc");
                if (hashFunc != null)
                {
                    var result = hashFunc.Invoke(opts, null);
                    if (result is long h)
                    {
                        hash = h;
                    }
                }
            }
            return Package.SignPKCS1v15(rand, this, hash, digest);
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) Decrypt(object? rand, Slice<byte> msg, [GoParam("crypto.DecrypterOpts")] object? opts)
        {
            return Package.DecryptPKCS1v15(rand, this, msg);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Validate()
        {
            // Basic validation — check we have RSA parameters
            if (_rsaParams == null && PublicKey._rsaParams == null)
            {
                return "crypto/rsa: missing key parameters";
            }
            return null;
        }

        [GoMethod]
        public void Precompute()
        {
            // .NET handles this internally
        }

        [GoField(Name = "Precomputed")]
        public GoPrecomputedValues Precomputed = new GoPrecomputedValues();

        internal RSA ToRSA()
        {
            var rsa = RSA.Create();
            if (_rsaParams.HasValue)
            {
                rsa.ImportParameters(_rsaParams.Value);
            }
            else if (PublicKey._rsaParams.HasValue)
            {
                rsa.ImportParameters(PublicKey._rsaParams.Value);
            }
            return rsa;
        }

        internal static GoPrivateKey FromRSA(RSA rsa)
        {
            var fullParams = rsa.ExportParameters(true);
            var pubParams = rsa.ExportParameters(false);

            var priv = new GoPrivateKey();
            priv._rsaParams = fullParams;
            priv.PublicKey = GoPublicKey.FromParameters(pubParams);
            priv.PublicKey._rsaParams = fullParams; // Keep full params for signing

            if (fullParams.D != null)
            {
                var d = new Math.Big.GoInt();
                d.SetBytes(new Slice<byte>(fullParams.D));
                priv.D = d;
            }

            return priv;
        }
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
