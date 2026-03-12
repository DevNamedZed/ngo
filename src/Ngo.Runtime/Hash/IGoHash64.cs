using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash
{
    // hash.Hash64 interface
    [GoType("interface", Name = "Hash64", Package = "hash")]
    public interface IGoHash64 : IGoHash
    {
        [GoMethod]
        long Sum64();
    }
}
