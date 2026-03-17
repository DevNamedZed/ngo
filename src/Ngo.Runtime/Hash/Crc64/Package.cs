using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Hash.Crc64
{
    [GoPackage("hash/crc64")]
    public static class Package
    {
        // crc64.New(tab *Table) hash.Hash64
        [GoFunc]
        [return: GoReturn("hash.Hash64")]
        public static object New(object? tab)
        {
            var table = tab as GoTable ?? ECMATableObj;
            return new Crc64Hash(table);
        }

        // crc64.Checksum(data []byte, tab *Table) uint64
        [GoFunc]
        public static ulong Checksum(Slice<byte> data, object? tab)
        {
            var table = tab as GoTable ?? ECMATableObj;
            ulong crc = 0xFFFFFFFFFFFFFFFF;
            for (int i = 0; i < data.Len; i++)
            {
                crc = table.Entries[(byte)(crc ^ data[i])] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFFFFFFFFFF;
        }

        // crc64.Update(crc uint64, tab *Table, p []byte) uint64
        [GoFunc]
        public static ulong Update(ulong crc, object? tab, Slice<byte> p)
        {
            var table = tab as GoTable ?? ECMATableObj;
            crc = ~crc;
            for (int i = 0; i < p.Len; i++)
            {
                crc = table.Entries[(byte)(crc ^ p[i])] ^ (crc >> 8);
            }
            return ~crc;
        }

        // crc64.MakeTable(poly uint64) *Table
        [GoFunc]
        [return: GoReturn("*crc64.Table")]
        public static object MakeTable(ulong poly)
        {
            return new GoTable(poly);
        }

        // Constants
        [GoConst(Type = "int")]
        public const long Size = 8;

        [GoConst(Type = "uint64")]
        public static readonly long ISO = unchecked((long)0xD800000000000000UL);

        [GoConst(Type = "uint64")]
        public static readonly long ECMA = unchecked((long)0xC96C5795D7870F42UL);

        private static readonly GoTable ECMATableObj = new GoTable(unchecked((ulong)ECMA));

        // Package vars
        [GoVar(Type = "*crc64.Table")]
        public static readonly object ISOTable = new GoTable(unchecked((ulong)ISO));

        [GoVar(Type = "*crc64.Table")]
        public static readonly object ECMATable = ECMATableObj;
    }

    [GoType("struct", Name = "Table", Package = "hash/crc64")]
    public class GoTable
    {
        internal readonly ulong[] Entries = new ulong[256];

        public GoTable() : this(unchecked((ulong)0xC96C5795D7870F42UL)) { }

        public GoTable(ulong poly)
        {
            for (int i = 0; i < 256; i++)
            {
                ulong crc = (ulong)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ poly;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
                Entries[i] = crc;
            }
        }
    }

    internal class Crc64Hash : IGoHash64
    {
        private readonly GoTable _table;
        private ulong _crc;

        public Crc64Hash(GoTable table)
        {
            _table = table;
            _crc = 0xFFFFFFFFFFFFFFFF;
        }

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
            {
                _crc = _table.Entries[(byte)(_crc ^ p[i])] ^ (_crc >> 8);
            }
            return (p.Len, null!);
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            ulong s = _crc ^ 0xFFFFFFFFFFFFFFFF;
            var result = new byte[] {
                (byte)(s >> 56), (byte)(s >> 48), (byte)(s >> 40), (byte)(s >> 32),
                (byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), (byte)s
            };
            return Slice<byte>.Append(b, result);
        }

        public void Reset()
        {
            _crc = 0xFFFFFFFFFFFFFFFF;
        }

        public long Size() => 8;
        public long BlockSize() => 1;

        public long Sum64()
        {
            return (long)(_crc ^ 0xFFFFFFFFFFFFFFFF);
        }
    }
}
