using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash.Crc32
{
    [GoPackage("hash/crc32")]
    public static class Package
    {
        // crc32.New(tab *Table) hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object New(object? tab) => throw new System.NotImplementedException();

        // crc32.NewIEEE() hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object NewIEEE() => throw new System.NotImplementedException();

        // crc32.ChecksumIEEE(data []byte) uint32
        [GoFunc]
        public static uint ChecksumIEEE(Slice<byte> data) => 0;

        // crc32.Checksum(data []byte, tab *Table) uint32
        [GoFunc]
        public static uint Checksum(Slice<byte> data, object? tab) => 0;

        // crc32.Update(crc uint32, tab *Table, p []byte) uint32
        [GoFunc]
        public static uint Update(uint crc, object? tab, Slice<byte> p) => 0;

        // crc32.MakeTable(poly uint32) *Table
        [GoFunc]
        [return: GoReturn("*crc32.Table")]
        public static object MakeTable(uint poly) => new GoTable();

        // Constants
        [GoConst(Type = "int")]
        public const long Size = 4;

        [GoConst(Type = "uint32")]
        public const long IEEE = unchecked((long)0xedb88320);

        [GoConst(Type = "uint32")]
        public const long Castagnoli = unchecked((long)0x82f63b78);

        [GoConst(Type = "uint32")]
        public const long Koopman = unchecked((long)0xeb31d82e);

        // Package var: IEEETable *Table
        [GoVar(Type = "*crc32.Table")]
        public static readonly object IEEETable = new GoTable();
    }

    [GoType("struct", Name = "Table", Package = "hash/crc32")]
    public class GoTable
    {
    }
}
