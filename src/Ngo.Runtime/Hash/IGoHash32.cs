using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash
{
    // hash.Hash32 interface
    [GoType("interface", Name = "Hash32", Package = "hash")]
    public interface IGoHash32 : IGoHash
    {
        [GoMethod]
        long Sum32();
    }
}
