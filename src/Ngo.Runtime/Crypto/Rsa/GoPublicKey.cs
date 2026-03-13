using System.Security.Cryptography;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Math.Big;

namespace Ngo.Runtime.Crypto.Rsa
{
    // rsa.PublicKey struct
    [GoType("struct", Name = "PublicKey", Package = "crypto/rsa")]
    public class GoPublicKey
    {
        [GoField(Name = "N")] public object? N; // *big.Int
        [GoField(Name = "E")] public long E;

        // Internal storage for the RSA parameters (so we don't lose precision going through big.Int)
        internal RSAParameters? _rsaParams;

        [GoMethod]
        [return: GoReturn("int")]
        public long Size()
        {
            if (_rsaParams.HasValue && _rsaParams.Value.Modulus != null)
            {
                return _rsaParams.Value.Modulus.Length;
            }
            if (N is GoInt bigN)
            {
                var bytes = bigN.Bytes();
                return bytes.Len;
            }
            return 0;
        }

        [GoMethod]
        public bool Equal([GoParam("crypto.PublicKey")] object? x)
        {
            if (x is GoPublicKey other)
            {
                return E == other.E && object.Equals(N, other.N);
            }
            return false;
        }

        internal RSA ToRSA()
        {
            var rsa = RSA.Create();
            if (_rsaParams.HasValue)
            {
                rsa.ImportParameters(_rsaParams.Value);
            }
            return rsa;
        }

        internal static GoPublicKey FromParameters(RSAParameters p)
        {
            var pub = new GoPublicKey();
            pub._rsaParams = p;
            if (p.Modulus != null)
            {
                var n = new GoInt();
                n.SetBytes(new Slice<byte>(p.Modulus));
                pub.N = n;
            }
            if (p.Exponent != null)
            {
                long e = 0;
                foreach (byte b in p.Exponent)
                {
                    e = (e << 8) | b;
                }
                pub.E = e;
            }
            return pub;
        }
    }
}
