using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash.Maphash
{
    [GoPackage("hash/maphash")]
    public static class Package
    {
        // maphash.Bytes(seed Seed, b []byte) uint64
        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong Bytes(GoSeed seed, Slice<byte> b) => 0;

        // maphash.String(seed Seed, s string) uint64
        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong String(GoSeed seed, string s) => 0;

        // maphash.MakeSeed() Seed
        [GoFunc]
        [return: GoReturn("maphash.Seed")]
        public static GoSeed MakeSeed() => new GoSeed();
    }

    // maphash.Seed struct
    [GoType("struct", Name = "Seed", Package = "hash/maphash")]
    public struct GoSeed { }

    // maphash.Hash struct
    [GoType("struct", Name = "Hash", Package = "hash/maphash")]
    public class GoHash
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> b) => (b.Len, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteByte(byte b) => null;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteString(string s) => (s.Length, null);

        [GoMethod]
        [return: GoReturn("uint64")]
        public ulong Sum64() => 0;

        [GoMethod]
        public void Reset() { }

        [GoMethod]
        public void SetSeed(GoSeed seed) { }

        [GoMethod]
        [return: GoReturn("maphash.Seed")]
        public GoSeed Seed() => new GoSeed();

        [GoMethod]
        [return: GoReturn("int")]
        public long Size() => 8;

        [GoMethod]
        [return: GoReturn("int")]
        public long BlockSize() => 64;

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Sum(Slice<byte> b) => b;
    }
}
