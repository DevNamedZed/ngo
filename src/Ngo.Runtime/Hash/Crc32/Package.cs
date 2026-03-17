using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Hash.Crc32
{
    [GoPackage("hash/crc32")]
    public static class Package
    {
        // crc32.New(tab *Table) hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object New(object? tab)
        {
            var table = tab as GoTable ?? IEEETableObj;
            return new Crc32Hash(table);
        }

        // crc32.NewIEEE() hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object NewIEEE()
        {
            return new Crc32Hash(IEEETableObj);
        }

        // crc32.ChecksumIEEE(data []byte) uint32
        [GoFunc]
        public static uint ChecksumIEEE(Slice<byte> data)
        {
            return Checksum(data, IEEETableObj);
        }

        // crc32.Checksum(data []byte, tab *Table) uint32
        [GoFunc]
        public static uint Checksum(Slice<byte> data, object? tab)
        {
            var table = tab as GoTable ?? IEEETableObj;
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Len; i++)
            {
                crc = table.Entries[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFF;
        }

        // crc32.Update(crc uint32, tab *Table, p []byte) uint32
        [GoFunc]
        public static uint Update(uint crc, object? tab, Slice<byte> p)
        {
            var table = tab as GoTable ?? IEEETableObj;
            crc = ~crc;
            for (int i = 0; i < p.Len; i++)
            {
                crc = table.Entries[(crc ^ p[i]) & 0xFF] ^ (crc >> 8);
            }
            return ~crc;
        }

        // crc32.MakeTable(poly uint32) *Table
        [GoFunc]
        [return: GoReturn("*crc32.Table")]
        public static object MakeTable(uint poly)
        {
            return new GoTable(poly);
        }

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
        private static readonly GoTable IEEETableObj = new GoTable((uint)IEEE);

        [GoVar(Type = "*crc32.Table")]
        public static readonly object IEEETable = IEEETableObj;
    }

    [GoType("struct", Name = "Table", Package = "hash/crc32")]
    public class GoTable
    {
        internal readonly uint[] Entries = new uint[256];

        public GoTable() : this(unchecked((uint)0xedb88320)) { }

        public GoTable(uint poly)
        {
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
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

    internal class Crc32Hash : IGoHash32
    {
        private readonly GoTable _table;
        private uint _crc;

        public Crc32Hash(GoTable table)
        {
            _table = table;
            _crc = 0xFFFFFFFF;
        }

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
            {
                _crc = _table.Entries[(_crc ^ p[i]) & 0xFF] ^ (_crc >> 8);
            }
            return (p.Len, null!);
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            uint s = _crc ^ 0xFFFFFFFF;
            var result = new byte[] {
                (byte)(s >> 24),
                (byte)(s >> 16),
                (byte)(s >> 8),
                (byte)s
            };
            return Slice<byte>.Append(b, result);
        }

        public void Reset()
        {
            _crc = 0xFFFFFFFF;
        }

        public long Size() => 4;
        public long BlockSize() => 1;

        public long Sum32()
        {
            return (long)(_crc ^ 0xFFFFFFFF);
        }
    }
}
