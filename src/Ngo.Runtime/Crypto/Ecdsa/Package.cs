using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Math.Big;

namespace Ngo.Runtime.Crypto.Ecdsa
{
    [GoPackage("crypto/ecdsa")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*ecdsa.PrivateKey", "error")]
        public static (object?, object?) GenerateKey(object? c, object? rand)
        {
            try
            {
                ECCurve curve = DetectCurve(c);
                using var ecdsa = ECDsa.Create(curve);
                var priv = GoPrivateKey.FromECDsa(ecdsa);
                return (priv, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoFunc]
        public static bool Verify([GoParam("*ecdsa.PublicKey")] object? pub, Slice<byte> hash, [GoParam("*big.Int")] object? r, [GoParam("*big.Int")] object? s)
        {
            if (pub is not GoPublicKey pubKey)
            {
                return false;
            }
            try
            {
                using var ecdsa = pubKey.ToECDsa();
                // Convert r,s big.Int to DER-encoded signature
                var rBytes = (r as GoInt)?.Bytes() ?? new Slice<byte>();
                var sBytes = (s as GoInt)?.Bytes() ?? new Slice<byte>();
                var sig = EncodeRSSignature(rBytes, sBytes);
                var hashBytes = SliceToArray(hash);
                return ecdsa.VerifyHash(hashBytes, sig, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            catch
            {
                return false;
            }
        }

        [GoFunc]
        public static bool VerifyASN1([GoParam("*ecdsa.PublicKey")] object? pub, Slice<byte> hash, Slice<byte> sig)
        {
            if (pub is not GoPublicKey pubKey)
            {
                return false;
            }
            try
            {
                using var ecdsa = pubKey.ToECDsa();
                var hashBytes = SliceToArray(hash);
                var sigBytes = SliceToArray(sig);
                return ecdsa.VerifyHash(hashBytes, sigBytes, DSASignatureFormat.Rfc3279DerSequence);
            }
            catch
            {
                return false;
            }
        }

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) SignASN1(object? rand, [GoParam("*ecdsa.PrivateKey")] object? priv, Slice<byte> hash)
        {
            if (priv is not GoPrivateKey privKey)
            {
                return (new Slice<byte>(), "crypto/ecdsa: nil private key");
            }
            try
            {
                using var ecdsa = privKey.ToECDsa();
                var hashBytes = SliceToArray(hash);
                var sig = ecdsa.SignHash(hashBytes, DSASignatureFormat.Rfc3279DerSequence);
                return (new Slice<byte>(sig), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        private static ECCurve DetectCurve(object? c)
        {
            if (c == null)
            {
                return ECCurve.NamedCurves.nistP256;
            }
            // Check curve type by name or Size method
            var typeName = c.GetType().Name;
            if (typeName.Contains("224") || typeName.Contains("P224"))
            {
                return ECCurve.NamedCurves.nistP256; // P-224 not widely supported, fall back
            }
            if (typeName.Contains("384") || typeName.Contains("P384"))
            {
                return ECCurve.NamedCurves.nistP384;
            }
            if (typeName.Contains("521") || typeName.Contains("P521"))
            {
                return ECCurve.NamedCurves.nistP521;
            }
            return ECCurve.NamedCurves.nistP256;
        }

        private static byte[] EncodeRSSignature(Slice<byte> r, Slice<byte> s)
        {
            // Create IEEE P1363 concatenated r||s
            int size = System.Math.Max(r.Len, s.Len);
            var sig = new byte[size * 2];
            int rOffset = size - r.Len;
            for (int i = 0; i < r.Len; i++)
            {
                sig[rOffset + i] = r[i];
            }
            int sOffset = size + (size - s.Len);
            for (int i = 0; i < s.Len; i++)
            {
                sig[sOffset + i] = s[i];
            }
            return sig;
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

    [GoType("struct", Name = "PublicKey", Package = "crypto/ecdsa")]
    public class GoPublicKey
    {
        [GoField(Name = "Curve", Embedded = true, Type = "elliptic.Curve")]
        public object? Curve;

        [GoField(Name = "X", Type = "*big.Int")] public object? X;
        [GoField(Name = "Y", Type = "*big.Int")] public object? Y;

        internal ECParameters? _ecParams;

        [GoMethod]
        public bool Equal([GoParam("crypto.PublicKey")] object? x)
        {
            if (x is GoPublicKey other)
            {
                if (_ecParams.HasValue && other._ecParams.HasValue)
                {
                    return _ecParams.Value.Q.X != null && other._ecParams.Value.Q.X != null &&
                           SpansEqual(_ecParams.Value.Q.X, other._ecParams.Value.Q.X) &&
                           SpansEqual(_ecParams.Value.Q.Y!, other._ecParams.Value.Q.Y!);
                }
            }
            return false;
        }

        [GoMethod]
        [return: GoReturn("*ecdh.PublicKey", "error")]
        public (object?, object?) ECDH() => (null, null);

        internal void SetFromParameters(ECParameters ecParams)
        {
            _ecParams = ecParams;
            if (ecParams.Q.X != null)
            {
                var xInt = new Math.Big.GoInt();
                xInt.SetBytes(new Slice<byte>(ecParams.Q.X));
                X = xInt;
            }
            if (ecParams.Q.Y != null)
            {
                var yInt = new Math.Big.GoInt();
                yInt.SetBytes(new Slice<byte>(ecParams.Q.Y));
                Y = yInt;
            }
        }

        internal ECDsa ToECDsa()
        {
            var ecdsa = ECDsa.Create();
            if (_ecParams.HasValue)
            {
                ecdsa.ImportParameters(_ecParams.Value);
            }
            return ecdsa;
        }

        private static bool SpansEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }

    [GoType("struct", Name = "PrivateKey", Package = "crypto/ecdsa")]
    public class GoPrivateKey
    {
        [GoField(Embedded = true)]
        public GoPublicKey PublicKey = new GoPublicKey();

        [GoField(Name = "D", Type = "*big.Int")] public object? D;

        internal ECParameters? _ecParams;

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
            return Package.SignASN1(rand, this, digest);
        }

        [GoMethod]
        [return: GoReturn("*ecdh.PrivateKey", "error")]
        public (object?, object?) ECDH() => (null, null);

        internal ECDsa ToECDsa()
        {
            var ecdsa = ECDsa.Create();
            if (_ecParams.HasValue)
            {
                ecdsa.ImportParameters(_ecParams.Value);
            }
            return ecdsa;
        }

        internal static GoPrivateKey FromECDsa(ECDsa ecdsa)
        {
            var fullParams = ecdsa.ExportParameters(true);
            var pubParams = ecdsa.ExportParameters(false);

            var priv = new GoPrivateKey();
            priv._ecParams = fullParams;

            var pub = new GoPublicKey();
            pub._ecParams = pubParams;
            if (pubParams.Q.X != null)
            {
                var x = new GoInt();
                x.SetBytes(new Slice<byte>(pubParams.Q.X));
                pub.X = x;
            }
            if (pubParams.Q.Y != null)
            {
                var y = new GoInt();
                y.SetBytes(new Slice<byte>(pubParams.Q.Y));
                pub.Y = y;
            }
            priv.PublicKey = pub;

            if (fullParams.D != null)
            {
                var d = new GoInt();
                d.SetBytes(new Slice<byte>(fullParams.D));
                priv.D = d;
            }

            return priv;
        }
    }
}
