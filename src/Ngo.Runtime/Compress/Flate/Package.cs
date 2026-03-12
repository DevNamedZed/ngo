using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Compress.Flate
{
    [GoPackage("compress/flate")]
    public static class Package
    {
        // flate.NewReader(r io.Reader) io.ReadCloser
        [GoFunc]
        [return: GoReturn("io.ReadCloser")]
        public static object NewReader(object? r) => throw new System.NotImplementedException();

        // flate.NewWriter(w io.Writer, level int) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*flate.Writer", "error")]
        public static (GoWriter, object?) NewWriter(object? w, [GoParam("int")] long level) => (new GoWriter(), null);

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

        // flate.NewReaderDict(r io.Reader, dict []byte) io.ReadCloser
        [GoFunc]
        [return: GoReturn("io.ReadCloser")]
        public static object NewReaderDict(object? r, Slice<byte> dict) => throw new System.NotImplementedException();

        // flate.NewWriterDict(w io.Writer, level int, dict []byte) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*flate.Writer", "error")]
        public static (GoWriter, object?) NewWriterDict(object? w, [GoParam("int")] long level, Slice<byte> dict) => (new GoWriter(), null);

        // flate.Reader interface { Read([]byte) (int, error); ReadByte() (byte, error) }
        [GoType("interface", Name = "Reader", Package = "compress/flate")]
        public interface IReader
        {
            [GoMethod]
            [return: GoReturn("int", "error")]
            (long, object?) Read(Slice<byte> p);

            [GoMethod]
            [return: GoReturn("byte", "error")]
            (byte, object?) ReadByte();
        }

        // flate.Resetter interface { Reset(r io.Reader, dict []byte) error }
        [GoType("interface", Name = "Resetter", Package = "compress/flate")]
        public interface IResetter
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Reset(object? r, Slice<byte> dict);
        }
    }

    [GoType("struct", Name = "Writer", Package = "compress/flate")]
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
