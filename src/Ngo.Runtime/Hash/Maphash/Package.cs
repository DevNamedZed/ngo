using System;
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash.Maphash
{
    [GoPackage("hash/maphash")]
    public static class Package
    {
        private static readonly Random _seedRng = new Random();

        // maphash.Bytes(seed Seed, b []byte) uint64
        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong Bytes(GoSeed seed, Slice<byte> b)
        {
            ulong hash = seed.Value;
            for (int i = 0; i < b.Len; i++)
            {
                hash ^= b[i];
                hash *= 0x100000001b3; // FNV-1a prime
            }
            return hash;
        }

        // maphash.String(seed Seed, s string) uint64
        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong String(GoSeed seed, string s)
        {
            ulong hash = seed.Value;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 0x100000001b3;
            }
            return hash;
        }

        // maphash.MakeSeed() Seed
        [GoFunc]
        [return: GoReturn("maphash.Seed")]
        public static GoSeed MakeSeed()
        {
            ulong seedValue;
            lock (_seedRng)
            {
                var buf = new byte[8];
                _seedRng.NextBytes(buf);
                seedValue = BitConverter.ToUInt64(buf, 0);
            }
            // Ensure non-zero
            if (seedValue == 0)
            {
                seedValue = 0xcbf29ce484222325; // FNV offset basis
            }
            return new GoSeed { Value = seedValue };
        }
    }

    // maphash.Seed struct
    [GoType("struct", Name = "Seed", Package = "hash/maphash")]
    public struct GoSeed
    {
        internal ulong Value;
    }

    // maphash.Hash struct
    [GoType("struct", Name = "Hash", Package = "hash/maphash")]
    public class GoHash
    {
        private ulong _hash;
        private GoSeed _seed;
        private bool _seeded;

        private void EnsureSeeded()
        {
            if (!_seeded)
            {
                _seed = Package.MakeSeed();
                _hash = _seed.Value;
                _seeded = true;
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> b)
        {
            EnsureSeeded();
            for (int i = 0; i < b.Len; i++)
            {
                _hash ^= b[i];
                _hash *= 0x100000001b3;
            }
            return (b.Len, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteByte(byte b)
        {
            EnsureSeeded();
            _hash ^= b;
            _hash *= 0x100000001b3;
            return null;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteString(string s)
        {
            EnsureSeeded();
            for (int i = 0; i < s.Length; i++)
            {
                _hash ^= s[i];
                _hash *= 0x100000001b3;
            }
            return (s.Length, null);
        }

        [GoMethod]
        [return: GoReturn("uint64")]
        public ulong Sum64()
        {
            EnsureSeeded();
            return _hash;
        }

        [GoMethod]
        public void Reset()
        {
            if (_seeded)
            {
                _hash = _seed.Value;
            }
        }

        [GoMethod]
        public void SetSeed(GoSeed seed)
        {
            _seed = seed;
            _hash = seed.Value;
            _seeded = true;
        }

        [GoMethod]
        [return: GoReturn("maphash.Seed")]
        public GoSeed Seed()
        {
            EnsureSeeded();
            return _seed;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Size() => 8;

        [GoMethod]
        [return: GoReturn("int")]
        public long BlockSize() => 64;

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Sum(Slice<byte> b)
        {
            ulong s = Sum64();
            var result = new byte[] {
                (byte)(s >> 56), (byte)(s >> 48), (byte)(s >> 40), (byte)(s >> 32),
                (byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), (byte)s
            };
            return Slice<byte>.Append(b, result);
        }
    }
}
