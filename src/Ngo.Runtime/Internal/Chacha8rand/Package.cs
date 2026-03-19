using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Chacha8rand
{
    [GoPackage("internal/chacha8rand")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) Marshal([GoParam("*State")] GoState? state) => (default, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unmarshal([GoParam("*State")] GoState? state, Slice<byte> data) => null;
    }

    [GoType("struct", Name = "State", Package = "internal/chacha8rand")]
    public class GoState
    {
        private readonly System.Random _rng = new();
        [GoMethod] public void Init(Slice<long> seed) { }
        [GoMethod] public void Init64(Slice<long> seed) { }
        [GoMethod] public void Refill() { }
        [GoMethod] [return: GoReturn("uint64")] public long Next() => (long)((ulong)_rng.NextInt64());
        [GoMethod] public void Reseed() { }
        [GoMethod] [return: GoReturn("error")] public object? Marshal() => null;
        [GoMethod] [return: GoReturn("error")] public object? Unmarshal(Slice<byte> data) => null;
    }

}
