using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Nistec
{
    [GoType("struct", Name = "P521Point", Package = "crypto/internal/nistec")]
    public class P521Point
    {
        [GoMethod]
        [return: GoReturn("*P521Point")]
        public P521Point SetGenerator() => this;

        [GoMethod]
        [return: GoReturn("*P521Point")]
        public P521Point Set(P521Point q) => this;

        [GoMethod]
        [return: GoReturn("*P521Point", "error")]
        public (P521Point, object?) SetBytes(Slice<byte> b) => (this, null);

        [GoMethod]
        public Slice<byte> Bytes() => default;

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) BytesX() => (default, null);

        [GoMethod]
        public Slice<byte> BytesCompressed() => default;

        [GoMethod]
        [return: GoReturn("*P521Point")]
        public P521Point Add(P521Point p1, P521Point p2) => this;

        [GoMethod]
        [return: GoReturn("*P521Point")]
        public P521Point Double(P521Point p) => this;

        [GoMethod]
        [return: GoReturn("*P521Point", "error")]
        public (P521Point, object?) ScalarMult(P521Point q, Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P521Point", "error")]
        public (P521Point, object?) ScalarBaseMult(Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P521Point")]
        public P521Point Select(P521Point p1, P521Point p2, [GoParam("int")] long cond) => this;
    }
}
