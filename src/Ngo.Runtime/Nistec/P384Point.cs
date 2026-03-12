using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Nistec
{
    [GoType("struct", Name = "P384Point", Package = "crypto/internal/nistec")]
    public class P384Point
    {
        [GoMethod]
        [return: GoReturn("*P384Point")]
        public P384Point SetGenerator() => this;

        [GoMethod]
        [return: GoReturn("*P384Point")]
        public P384Point Set(P384Point q) => this;

        [GoMethod]
        [return: GoReturn("*P384Point", "error")]
        public (P384Point, object?) SetBytes(Slice<byte> b) => (this, null);

        [GoMethod]
        public Slice<byte> Bytes() => default;

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) BytesX() => (default, null);

        [GoMethod]
        public Slice<byte> BytesCompressed() => default;

        [GoMethod]
        [return: GoReturn("*P384Point")]
        public P384Point Add(P384Point p1, P384Point p2) => this;

        [GoMethod]
        [return: GoReturn("*P384Point")]
        public P384Point Double(P384Point p) => this;

        [GoMethod]
        [return: GoReturn("*P384Point", "error")]
        public (P384Point, object?) ScalarMult(P384Point q, Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P384Point", "error")]
        public (P384Point, object?) ScalarBaseMult(Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P384Point")]
        public P384Point Select(P384Point p1, P384Point p2, [GoParam("int")] long cond) => this;
    }
}
