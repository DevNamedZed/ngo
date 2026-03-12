using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Binary
{
    [GoPackage("encoding/binary")]
    public static class Package
    {
        // ByteOrder interface
        [GoType("interface", Name = "ByteOrder", Package = "encoding/binary")]
        public interface IByteOrder
        {
            [GoMethod]
            ushort Uint16(Slice<byte> b);
            [GoMethod]
            uint Uint32(Slice<byte> b);
            [GoMethod]
            ulong Uint64(Slice<byte> b);
            [GoMethod]
            void PutUint16(Slice<byte> b, ushort v);
            [GoMethod]
            void PutUint32(Slice<byte> b, uint v);
            [GoMethod]
            void PutUint64(Slice<byte> b, ulong v);
            [GoMethod]
            string String();
        }

        // BigEndian and LittleEndian vars
        [GoVar(Type = "encoding/binary.bigEndian")]
        public static readonly GoBigEndian BigEndian = new GoBigEndian();

        [GoVar(Type = "encoding/binary.littleEndian")]
        public static readonly GoLittleEndian LittleEndian = new GoLittleEndian();

        // binary.Read(r io.Reader, order ByteOrder, data interface{}) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Read(object? r, object? order, object? data) => null;

        // binary.Write(w io.Writer, order ByteOrder, data interface{}) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Write(object? w, object? order, object? data) => null;

        // binary.Size(v interface{}) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long Size(object? v) => 0;

        // binary.PutVarint(buf []byte, x int64) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long PutVarint(Slice<byte> buf, long x) => 0;

        // binary.PutUvarint(buf []byte, x uint64) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long PutUvarint(Slice<byte> buf, ulong x) => 0;

        // binary.Varint(buf []byte) (int64, int)
        [GoFunc]
        [return: GoReturn("int64", "int")]
        public static (long, long) Varint(Slice<byte> buf) => (0, 0);

        // binary.Uvarint(buf []byte) (uint64, int)
        [GoFunc]
        [return: GoReturn("uint64", "int")]
        public static (ulong, long) Uvarint(Slice<byte> buf) => (0, 0);

        // binary.ReadUvarint(r io.ByteReader) (uint64, error)
        [GoFunc]
        [return: GoReturn("uint64", "error")]
        public static (ulong, object?) ReadUvarint([GoParam("io.ByteReader")] object? r) => (0, null);

        // Constants
        [GoConst(Type = "int")]
        public const long MaxVarintLen16 = 3;

        [GoConst(Type = "int")]
        public const long MaxVarintLen32 = 5;

        [GoConst(Type = "int")]
        public const long MaxVarintLen64 = 10;
    }

    [GoType("struct", Name = "bigEndian", Package = "encoding/binary")]
    public class GoBigEndian : Encoding.Binary.Package.IByteOrder
    {
        [GoMethod] public ushort Uint16(Slice<byte> b) => 0;
        [GoMethod] public uint Uint32(Slice<byte> b) => 0;
        [GoMethod] public ulong Uint64(Slice<byte> b) => 0;
        [GoMethod] public void PutUint16(Slice<byte> b, ushort v) { }
        [GoMethod] public void PutUint32(Slice<byte> b, uint v) { }
        [GoMethod] public void PutUint64(Slice<byte> b, ulong v) { }
        [GoMethod] public string String() => "BigEndian";
        [GoMethod] public Slice<byte> AppendUint16(Slice<byte> b, ushort v) => b;
        [GoMethod] public Slice<byte> AppendUint32(Slice<byte> b, uint v) => b;
        [GoMethod] public Slice<byte> AppendUint64(Slice<byte> b, ulong v) => b;
    }

    [GoType("struct", Name = "littleEndian", Package = "encoding/binary")]
    public class GoLittleEndian : Encoding.Binary.Package.IByteOrder
    {
        [GoMethod] public ushort Uint16(Slice<byte> b) => 0;
        [GoMethod] public uint Uint32(Slice<byte> b) => 0;
        [GoMethod] public ulong Uint64(Slice<byte> b) => 0;
        [GoMethod] public void PutUint16(Slice<byte> b, ushort v) { }
        [GoMethod] public void PutUint32(Slice<byte> b, uint v) { }
        [GoMethod] public void PutUint64(Slice<byte> b, ulong v) { }
        [GoMethod] public string String() => "LittleEndian";
        [GoMethod] public Slice<byte> AppendUint16(Slice<byte> b, ushort v) => b;
        [GoMethod] public Slice<byte> AppendUint32(Slice<byte> b, uint v) => b;
        [GoMethod] public Slice<byte> AppendUint64(Slice<byte> b, ulong v) => b;
    }
}
