using System.Numerics;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Rand
{
    [GoPackage("crypto/rand")]
    public static class Package
    {
        [GoVar(Type = "io.Reader")]
        public static readonly object Reader = new CryptoRandReader();

        public static (long, object?) Read(Slice<byte> b)
        {
            var arr = new byte[(int)b.Len];
            RandomNumberGenerator.Fill(arr);
            for (int i = 0; i < arr.Length; i++)
                b[i] = arr[i];
            return ((long)arr.Length, null);
        }

        [GoFunc]
        [return: GoReturn("*big.Int", "error")]
        public static (BigInteger, object?) Prime([GoParam("io.Reader")] object reader, long bits)
        {
            // Generate a random prime of the given bit length
            var bytes = new byte[bits / 8 + 1];
            BigInteger result;
            do
            {
                RandomNumberGenerator.Fill(bytes);
                bytes[bytes.Length - 1] &= 0x7F; // ensure positive
                bytes[0] |= 0x01; // ensure odd
                result = new BigInteger(bytes);
                if (result < 0) result = -result;
            } while (!IsProbablyPrime(result, 20));
            return (result, null);
        }

        private static bool IsProbablyPrime(BigInteger n, int k)
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if (n % 2 == 0) return false;
            BigInteger d = n - 1;
            int r = 0;
            while (d % 2 == 0) { d /= 2; r++; }
            var rng = new byte[n.GetByteCount()];
            for (int i = 0; i < k; i++)
            {
                RandomNumberGenerator.Fill(rng);
                rng[rng.Length - 1] &= 0x7F;
                BigInteger a = new BigInteger(rng) % (n - 3) + 2;
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) continue;
                bool found = false;
                for (int j = 0; j < r - 1; j++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == n - 1) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        [GoFunc]
        [return: GoReturn("*big.Int", "error")]
        public static (BigInteger, object?) Int([GoParam("io.Reader")] object reader, [GoParam("*big.Int")] BigInteger max)
        {
            // Simplified: use system crypto RNG
            var bytes = max.ToByteArray();
            BigInteger result;
            do
            {
                RandomNumberGenerator.Fill(bytes);
                bytes[bytes.Length - 1] &= 0x7F; // ensure positive
                result = new BigInteger(bytes);
            } while (result >= max || result < 0);
            return (result, null);
        }
    }
}
