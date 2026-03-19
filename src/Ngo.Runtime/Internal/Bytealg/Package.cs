using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Bytealg
{
    /// <summary>
    /// internal/bytealg — byte/string search algorithms.
    /// Assembly-backed functions are handled by RuntimeIntrinsics.
    /// This stub provides constants and pure-Go-compatible signatures.
    /// </summary>
    [GoPackage("internal/bytealg")]
    public static class Package
    {
        [GoConst] public const long MaxLen = 0; // triggers generic fallback in Go source
        [GoConst] public const long MaxBruteForce = 64;
        [GoConst] public const long PrimeRK = 16777619;

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexByte(Slice<byte> b, byte c)
        {
            for (int i = 0; i < b.Len; i++)
                if (b[i] == c) return i;
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexByteString(string s, byte c)
        {
            for (int i = 0; i < s.Length; i++)
                if ((byte)s[i] == c) return i;
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexString(string s, string substr)
        {
            return s.IndexOf(substr, System.StringComparison.Ordinal);
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Index(Slice<byte> a, Slice<byte> b)
        {
            if (b.Len == 0) return 0;
            if (b.Len > a.Len) return -1;
            for (int i = 0; i <= a.Len - b.Len; i++)
            {
                bool match = true;
                for (int j = 0; j < b.Len; j++)
                    if (a[i + j] != b[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Count(Slice<byte> b, byte c)
        {
            int n = 0;
            for (int i = 0; i < b.Len; i++)
                if (b[i] == c) n++;
            return n;
        }

        [GoFunc]
        public static bool Equal(Slice<byte> a, Slice<byte> b)
        {
            if (a.Len != b.Len) return false;
            for (int i = 0; i < a.Len; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Compare(Slice<byte> a, Slice<byte> b)
        {
            int n = System.Math.Min(a.Len, b.Len);
            for (int i = 0; i < n; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            return a.Len < b.Len ? -1 : a.Len > b.Len ? 1 : 0;
        }

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (long, long) HashStr(string sep) => (0, 0);

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (long, long) HashStrBytes(Slice<byte> sep) => (0, 0);

        [GoFunc]
        [return: GoReturn("int")]
        public static long Cutover([GoParam("int")] long n) => n;

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexRabinKarp(string s, string substr) => (long)s.IndexOf(substr, System.StringComparison.Ordinal);

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexRabinKarp(Slice<byte> s, Slice<byte> sep) => Index(s, sep);

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexRabinKarpBytes(Slice<byte> s, Slice<byte> sep) => Index(s, sep);

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexRabinKarp(Slice<byte> s, Slice<byte> sep)
        {
            if (sep.Len == 0) return s.Len;
            if (sep.Len > s.Len) return -1;
            for (int i = s.Len - sep.Len; i >= 0; i--)
            {
                bool match = true;
                for (int j = 0; j < sep.Len; j++)
                    if (s[i + j] != sep[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long CountString(string s, byte c)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
                if ((byte)s[i] == c) count++;
            return count;
        }

        // Note: Go 1.22 CountString(s string, c byte) takes a byte, not string

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexByte(Slice<byte> s, byte c)
        {
            for (int i = s.Len - 1; i >= 0; i--)
                if (s[i] == c) return i;
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexByteString(string s, byte c)
        {
            for (int i = s.Length - 1; i >= 0; i--)
                if ((byte)s[i] == c) return i;
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexRabinKarp(string s, string substr) => (long)s.LastIndexOf(substr, System.StringComparison.Ordinal);

        // HashStrRev has string overload (used by strings package)
        // and byte slice overload (used by bytes package).
        // Go doesn't have overloading — these are separate functions in Go
        // (HashStrRev for string, HashStrRevBytes for []byte)
        // but ngo's bytealg stub merges them.
        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (long, long) HashStrRev(string sep) => (0, 0);

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (long, long) HashStrRevBytes(Slice<byte> sep) => (0, 0);

        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> MakeNoZero([GoParam("int")] long n) => new Slice<byte>(new byte[(int)n]);
    }
}
