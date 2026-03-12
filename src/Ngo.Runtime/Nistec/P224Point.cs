using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Nistec
{
    [GoType("struct", Name = "P224Point", Package = "crypto/internal/nistec")]
    public class P224Point
    {
        [GoMethod]
        [return: GoReturn("*P224Point")]
        public P224Point SetGenerator() => this;

        [GoMethod]
        [return: GoReturn("*P224Point")]
        public P224Point Set(P224Point q) => this;

        [GoMethod]
        [return: GoReturn("*P224Point", "error")]
        public (P224Point, object?) SetBytes(Slice<byte> b) => (this, null);

        [GoMethod]
        public Slice<byte> Bytes() => default;

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) BytesX() => (default, null);

        [GoMethod]
        public Slice<byte> BytesCompressed() => default;

        [GoMethod]
        [return: GoReturn("*P224Point")]
        public P224Point Add(P224Point p1, P224Point p2) => this;

        [GoMethod]
        [return: GoReturn("*P224Point")]
        public P224Point Double(P224Point p) => this;

        [GoMethod]
        [return: GoReturn("*P224Point", "error")]
        public (P224Point, object?) ScalarMult(P224Point q, Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P224Point", "error")]
        public (P224Point, object?) ScalarBaseMult(Slice<byte> scalar) => (this, null);

        [GoMethod]
        [return: GoReturn("*P224Point")]
        public P224Point Select(P224Point p1, P224Point p2, [GoParam("int")] long cond) => this;
    }
}
