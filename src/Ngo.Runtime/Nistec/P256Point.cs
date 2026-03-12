using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Nistec
{
    [GoType("struct", Name = "P256Point", Package = "crypto/internal/nistec")]
    public class P256Point
    {
        [GoMethod]
        [return: GoReturn("*P256Point")]
        public P256Point SetGenerator() => this;

        [GoMethod]
        [return: GoReturn("*P256Point")]
        public P256Point Set(P256Point q) => this;

        [GoMethod]
        [return: GoReturn("*P256Point", "error")]
        public (P256Point, object?) SetBytes(Slice<byte> b) => (this, null);

        [GoMethod]
        public Slice<byte> Bytes() => default;

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) BytesX() => (default, null);

        [GoMethod]
        public Slice<byte> BytesCompressed() => default;

        [GoMethod]
        [return: GoReturn("*P256Point")]
        public P256Point Add(P256Point p1, P256Point p2) => this;

        [GoMethod]
        [return: GoReturn("*P256Point")]
        public P256Point Double(P256Point p) => this;

        [GoMethod]
        [return: GoReturn("*P256Point", "error")]
        public (P256Point, object?) ScalarMult(P256Point q, Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P256Point", "error")]
        public (P256Point, object?) ScalarBaseMult(Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P256Point")]
        public P256Point Select(P256Point p1, P256Point p2, [GoParam("int")] long cond) => this;
    }
}
