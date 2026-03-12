using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash.Crc64
{
    [GoPackage("hash/crc64")]
    public static class Package
    {
        // crc64.New(tab *Table) hash.Hash64
        [GoFunc]
        [return: GoReturn("hash.Hash64")]
        public static object New(object? tab) => throw new System.NotImplementedException();

        // crc64.Checksum(data []byte, tab *Table) uint64
        [GoFunc]
        public static ulong Checksum(Slice<byte> data, object? tab) => 0;

        // crc64.MakeTable(poly uint64) *Table
        [GoFunc]
        [return: GoReturn("*crc64.Table")]
        public static object MakeTable(ulong poly) => new GoTable();

        // Constants
        [GoConst(Type = "int")]
        public const long Size = 8;

        [GoConst(Type = "uint64")]
        public static readonly long ISO = unchecked((long)0xD800000000000000);

        [GoConst(Type = "uint64")]
        public static readonly long ECMA = unchecked((long)0xC96C5795D7870F42UL);
    }

    [GoType("struct", Name = "Table", Package = "hash/crc64")]
    public class GoTable
    {
    }
}
