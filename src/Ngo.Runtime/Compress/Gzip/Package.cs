using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Compress.Gzip
{
    [GoPackage("compress/gzip")]
    public static class Package
    {
        // gzip.NewWriter(w io.Writer) *Writer
        [GoFunc]
        [return: GoReturn("*gzip.Writer")]
        public static GoWriter NewWriter(object? w) => new GoWriter();

        // gzip.NewWriterLevel(w io.Writer, level int) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*gzip.Writer", "error")]
        public static (GoWriter, object?) NewWriterLevel(object? w, [GoParam("int")] long level) => (new GoWriter(), null);

        // gzip.NewReader(r io.Reader) (*Reader, error)
        [GoFunc]
        [return: GoReturn("*gzip.Reader", "error")]
        public static (GoReader, object?) NewReader(object? r) => (new GoReader(), null);

        // Compression level constants
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

    [GoType("struct", Name = "Writer", Package = "compress/gzip")]
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

    [GoType("struct", Name = "Reader", Package = "compress/gzip")]
    public class GoReader
    {
        [GoField(Name = "Header")]
        public object? Header;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> p) => (0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Reset(object? r) => null;
    }
}
