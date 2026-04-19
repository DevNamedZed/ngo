using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Math.Big;

namespace Ngo.Runtime.Crypto.Elliptic
{
    [GoPackage("crypto/elliptic")]
    public static class Package
    {
        // elliptic.Curve interface
        [GoType("interface", Name = "Curve", Package = "crypto/elliptic")]
        public interface ICurve
        {
            [GoMethod]
            [return: GoReturn("*elliptic.CurveParams")]
            object? Params();

            [GoMethod]
            bool IsOnCurve([GoParam("*math/big.Int")] object? x, [GoParam("*math/big.Int")] object? y);

            [GoMethod]
            [return: GoReturn("*math/big.Int", "*math/big.Int")]
            (object?, object?) Add([GoParam("*math/big.Int")] object? x1, [GoParam("*math/big.Int")] object? y1, [GoParam("*math/big.Int")] object? x2, [GoParam("*math/big.Int")] object? y2);

            [GoMethod]
            [return: GoReturn("*math/big.Int", "*math/big.Int")]
            (object?, object?) Double([GoParam("*math/big.Int")] object? x1, [GoParam("*math/big.Int")] object? y1);

            [GoMethod]
            [return: GoReturn("*math/big.Int", "*math/big.Int")]
            (object?, object?) ScalarMult([GoParam("*math/big.Int")] object? Bx, [GoParam("*math/big.Int")] object? By, Slice<byte> k);

            [GoMethod]
            [return: GoReturn("*math/big.Int", "*math/big.Int")]
            (object?, object?) ScalarBaseMult(Slice<byte> k);
        }

        private static readonly EllipticCurveImpl _p256 = new EllipticCurveImpl(ECCurve.NamedCurves.nistP256, "P-256", 256);
        private static readonly EllipticCurveImpl _p384 = new EllipticCurveImpl(ECCurve.NamedCurves.nistP384, "P-384", 384);
        private static readonly EllipticCurveImpl _p521 = new EllipticCurveImpl(ECCurve.NamedCurves.nistP521, "P-521", 521);

        // elliptic.P224() Curve — not widely supported in .NET, return P256 as fallback
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P224() => _p256;

        // elliptic.P256() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P256() => _p256;

        // elliptic.P384() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P384() => _p384;

        // elliptic.P521() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P521() => _p521;

        // elliptic.GenerateKey(curve Curve, rand io.Reader) (priv []byte, x, y *big.Int, err error)
        [GoFunc]
        [return: GoReturn("[]byte", "*math/big.Int", "*math/big.Int", "error")]
        public static (Slice<byte>, object?, object?, object?) GenerateKey([GoParam("elliptic.Curve")] object? curve, object? rand)
        {
            try
            {
                ECCurve ecCurve = ECCurve.NamedCurves.nistP256;
                if (curve is EllipticCurveImpl impl)
                {
                    ecCurve = impl.NetCurve;
                }

                using var ecdsa = ECDsa.Create(ecCurve);
                var param = ecdsa.ExportParameters(true);

                var priv = new Slice<byte>(param.D!);
                var x = new GoInt();
                x.SetBytes(new Slice<byte>(param.Q.X!));
                var y = new GoInt();
                y.SetBytes(new Slice<byte>(param.Q.Y!));
                return (priv, x, y, null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), null, null, (object)ex.Message);
            }
        }

        // elliptic.Marshal(curve Curve, x, y *big.Int) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> Marshal([GoParam("elliptic.Curve")] object? curve, [GoParam("*math/big.Int")] object? x, [GoParam("*math/big.Int")] object? y)
        {
            if (x is not GoInt bigX || y is not GoInt bigY)
            {
                return new Slice<byte>();
            }

            var xBytes = bigX.Bytes();
            var yBytes = bigY.Bytes();

            int byteLen = 32; // default for P-256
            if (curve is EllipticCurveImpl impl)
            {
                byteLen = ((int)impl.CurveParams.BitSize + 7) / 8;
            }

            // Uncompressed point format: 0x04 || X || Y
            var result = new byte[1 + byteLen * 2];
            result[0] = 0x04;
            CopyPadded(xBytes, result, 1, byteLen);
            CopyPadded(yBytes, result, 1 + byteLen, byteLen);
            return new Slice<byte>(result);
        }

        // elliptic.Unmarshal(curve Curve, data []byte) (x, y *big.Int)
        [GoFunc]
        [return: GoReturn("*math/big.Int", "*math/big.Int")]
        public static (object?, object?) Unmarshal([GoParam("elliptic.Curve")] object? curve, Slice<byte> data)
        {
            if (data.Len == 0 || data[0] != 0x04)
            {
                return (null, null);
            }

            int byteLen = (data.Len - 1) / 2;
            if (1 + byteLen * 2 != data.Len)
            {
                return (null, null);
            }

            var xBytes = new byte[byteLen];
            var yBytes = new byte[byteLen];
            for (int i = 0; i < byteLen; i++)
            {
                xBytes[i] = data[1 + i];
                yBytes[i] = data[1 + byteLen + i];
            }

            var x = new GoInt();
            x.SetBytes(new Slice<byte>(xBytes));
            var y = new GoInt();
            y.SetBytes(new Slice<byte>(yBytes));
            return (x, y);
        }

        private static void CopyPadded(Slice<byte> src, byte[] dst, int dstOffset, int fieldLen)
        {
            int srcLen = src.Len;
            int padLen = fieldLen - srcLen;
            if (padLen < 0)
            {
                padLen = 0;
            }
            for (int i = 0; i < srcLen && (dstOffset + padLen + i) < dst.Length; i++)
            {
                dst[dstOffset + padLen + i] = src[i];
            }
        }
    }

    // Internal implementation of the Curve interface backed by .NET ECCurve
    internal class EllipticCurveImpl : Package.ICurve
    {
        internal readonly ECCurve NetCurve;
        internal readonly GoCurveParams CurveParams;

        public EllipticCurveImpl(ECCurve curve, string name, int bitSize)
        {
            NetCurve = curve;
            CurveParams = new GoCurveParams
            {
                Name = name,
                BitSize = bitSize
            };
        }

        public object? Params()
        {
            return CurveParams;
        }

        public bool IsOnCurve(object? x, object? y)
        {
            // Validate by trying to create a key with these coordinates
            if (x is not GoInt bigX || y is not GoInt bigY)
            {
                return false;
            }
            try
            {
                var xBytes = ToByteArray(bigX.Bytes());
                var yBytes = ToByteArray(bigY.Bytes());
                var param = new ECParameters
                {
                    Curve = NetCurve,
                    Q = new ECPoint { X = xBytes, Y = yBytes }
                };
                param.Validate();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public (object?, object?) Add(object? x1, object? y1, object? x2, object? y2)
        {
            // EC point addition is not directly exposed in .NET
            // Return null to indicate unsupported (most Go code uses higher-level APIs)
            return (null, null);
        }

        public (object?, object?) Double(object? x1, object? y1)
        {
            return (null, null);
        }

        public (object?, object?) ScalarMult(object? Bx, object? By, Slice<byte> k)
        {
            return (null, null);
        }

        public (object?, object?) ScalarBaseMult(Slice<byte> k)
        {
            return (null, null);
        }

        private static byte[] ToByteArray(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
            {
                arr[i] = s[i];
            }
            return arr;
        }
    }

    // elliptic.CurveParams struct
    [GoType("struct", Name = "CurveParams", Package = "crypto/elliptic")]
    public class GoCurveParams : Package.ICurve
    {
        [GoField(Name = "P", Type = "*big.Int")] public object? P;
        [GoField(Name = "N", Type = "*big.Int")] public object? N;
        [GoField(Name = "B", Type = "*big.Int")] public object? B;
        [GoField(Name = "Gx", Type = "*big.Int")] public object? Gx;
        [GoField(Name = "Gy", Type = "*big.Int")] public object? Gy;
        [GoField(Name = "BitSize")] public long BitSize;
        [GoField(Name = "Name")] public string Name = "";

        [GoMethod]
        [return: GoReturn("*elliptic.CurveParams")]
        public object? Params() => this;

        [GoMethod]
        public bool IsOnCurve([GoParam("*math/big.Int")] object? x, [GoParam("*math/big.Int")] object? y) => false;

        [GoMethod]
        [return: GoReturn("*math/big.Int", "*math/big.Int")]
        public (object?, object?) Add([GoParam("*math/big.Int")] object? x1, [GoParam("*math/big.Int")] object? y1, [GoParam("*math/big.Int")] object? x2, [GoParam("*math/big.Int")] object? y2) => (null, null);

        [GoMethod]
        [return: GoReturn("*math/big.Int", "*math/big.Int")]
        public (object?, object?) Double([GoParam("*math/big.Int")] object? x1, [GoParam("*math/big.Int")] object? y1) => (null, null);

        [GoMethod]
        [return: GoReturn("*math/big.Int", "*math/big.Int")]
        public (object?, object?) ScalarMult([GoParam("*math/big.Int")] object? Bx, [GoParam("*math/big.Int")] object? By, Slice<byte> k) => (null, null);

        [GoMethod]
        [return: GoReturn("*math/big.Int", "*math/big.Int")]
        public (object?, object?) ScalarBaseMult(Slice<byte> k) => (null, null);
    }
}
