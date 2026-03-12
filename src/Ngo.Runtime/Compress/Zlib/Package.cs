using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Compress.Zlib
{
    [GoPackage("compress/zlib")]
    public static class Package
    {
        // zlib.NewReader(r io.Reader) (io.ReadCloser, error)
        [GoFunc]
        [return: GoReturn("io.ReadCloser", "error")]
        public static (object?, object?) NewReader(object? r) => (null, null);

        // zlib.NewWriter(w io.Writer) *Writer
        [GoFunc]
        [return: GoReturn("*zlib.Writer")]
        public static GoWriter NewWriter(object? w) => new GoWriter();

        // zlib.NewWriterLevel(w io.Writer, level int) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*zlib.Writer", "error")]
        public static (GoWriter, object?) NewWriterLevel(object? w, [GoParam("int")] long level) => (new GoWriter(), null);

        // Constants
        [GoConst(Type = "int")]
        public const long NoCompression = 0;

        [GoConst(Type = "int")]
        public const long BestSpeed = 1;

        [GoConst(Type = "int")]
        public const long BestCompression = 9;

        [GoConst(Type = "int")]
        public const long DefaultCompression = -1;

        [GoConst(Type = "int")]
        public const long HuffmanOnly = -2;
    }

    [GoType("struct", Name = "Writer", Package = "compress/zlib")]
    public class GoWriter
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p) => (0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => null;

        [GoMethod]
        public void Reset(object? w) { }
    }
}
