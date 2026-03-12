using Ngo.Runtime.Discovery;

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

        // elliptic.P224() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P224() => null;

        // elliptic.P256() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P256() => null;

        // elliptic.P384() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P384() => null;

        // elliptic.P521() Curve
        [GoFunc]
        [return: GoReturn("elliptic.Curve")]
        public static object? P521() => null;

        // elliptic.GenerateKey(curve Curve, rand io.Reader) (priv []byte, x, y *big.Int, err error)
        [GoFunc]
        [return: GoReturn("[]byte", "*math/big.Int", "*math/big.Int", "error")]
        public static (Slice<byte>, object?, object?, object?) GenerateKey([GoParam("elliptic.Curve")] object? curve, object? rand)
            => (new Slice<byte>(), null, null, null);

        // elliptic.Marshal(curve Curve, x, y *big.Int) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> Marshal([GoParam("elliptic.Curve")] object? curve, [GoParam("*math/big.Int")] object? x, [GoParam("*math/big.Int")] object? y)
            => new Slice<byte>();

        // elliptic.Unmarshal(curve Curve, data []byte) (x, y *big.Int)
        [GoFunc]
        [return: GoReturn("*math/big.Int", "*math/big.Int")]
        public static (object?, object?) Unmarshal([GoParam("elliptic.Curve")] object? curve, Slice<byte> data)
            => (null, null);
    }

    // elliptic.CurveParams struct
    [GoType("struct", Name = "CurveParams", Package = "crypto/elliptic")]
    public class GoCurveParams
    {
        [GoField(Name = "P", Type = "*big.Int")] public object? P;
        [GoField(Name = "N", Type = "*big.Int")] public object? N;
        [GoField(Name = "B", Type = "*big.Int")] public object? B;
        [GoField(Name = "Gx", Type = "*big.Int")] public object? Gx;
        [GoField(Name = "Gy", Type = "*big.Int")] public object? Gy;
        [GoField(Name = "BitSize")] public long BitSize;
        [GoField(Name = "Name")] public string Name = "";
    }
}
