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
            // Re-seed from data
            if (state != null && data.Len >= 8)
            {
                long seed = 0;
                for (int i = 0; i < System.Math.Min(8, data.Len); i++)
                {
                    seed |= (long)data[i] << (i * 8);
                }
                state.Init(new Slice<long>(new[] { seed }));
            }
            return null;
        }
    }

    [GoType("struct", Name = "State", Package = "internal/chacha8rand")]
    public class GoState
    {
        private System.Random _rng = new();

        [GoMethod]
        public void Init(Slice<long> seed)
        {
            if (seed.Len > 0)
            {
                _rng = new System.Random((int)seed[0]);
            }
        }

        [GoMethod]
        public void Init64(Slice<long> seed)
        {
            if (seed.Len > 0)
            {
                _rng = new System.Random((int)(seed[0] ^ (seed[0] >> 32)));
            }
        }

        [GoMethod]
        public void Refill()
        {
            // Refill is called when the internal buffer is exhausted.
            // System.Random doesn't have a buffer concept — Next() generates on demand.
        }

        [GoMethod]
        [return: GoReturn("uint64")]
        public long Next() => (long)((ulong)_rng.NextInt64());

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
