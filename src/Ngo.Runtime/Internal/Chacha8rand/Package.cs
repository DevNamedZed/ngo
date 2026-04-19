using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Chacha8rand
{
    [GoPackage("internal/chacha8rand")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) Marshal([GoParam("*State")] GoState? state)
        {
            // Return 32 bytes of state (ChaCha8 key size)
            var stateBytes = new byte[32];
            if (state != null)
            {
                System.Security.Cryptography.RandomNumberGenerator.Fill(stateBytes);
            }
            return (new Slice<byte>(stateBytes), null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unmarshal([GoParam("*State")] GoState? state, Slice<byte> data)
        {
            if (state != null && data.Len > 0)
            {
                var seed = new byte[32];
                int copyLength = System.Math.Min(seed.Length, data.Len);
                for (int index = 0; index < copyLength; index++)
                {
                    seed[index] = data[index];
                }
                state.Init(seed);
            }
            return null;
        }
    }

    [GoType("struct", Name = "State", Package = "internal/chacha8rand")]
    public class GoState
    {
        private System.Random _rng = new();

        [GoMethod]
        public void Init([GoParam("[32]byte")] byte[] seed)
        {
            int derivedSeed = 0;
            for (int index = 0; index < seed.Length; index++)
            {
                derivedSeed = unchecked((derivedSeed * 31) ^ seed[index]);
            }
            _rng = new System.Random(derivedSeed);
        }

        [GoMethod]
        public void Init64([GoParam("[4]uint64")] ulong[] seed)
        {
            ulong combined = 0;
            for (int index = 0; index < seed.Length; index++)
            {
                combined ^= seed[index];
            }
            _rng = new System.Random((int)(combined ^ (combined >> 32)));
        }

        [GoMethod]
        public void Refill()
        {
        }

        [GoMethod]
        [return: GoReturn("uint64", "bool")]
        public (long, bool) Next()
        {
            return ((long)((ulong)_rng.NextInt64()), true);
        }

        [GoMethod]
        public void Reseed()
        {
            _rng = new System.Random(System.Environment.TickCount);
        }
        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) Marshal()
        {
            return Package.Marshal(this);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Unmarshal(Slice<byte> data)
        {
            return Package.Unmarshal(this, data);
        }
    }

}
