using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Hash
{
    // hash.Hash interface
    [GoType("interface", Name = "Hash", Package = "hash")]
    public interface IGoHash : IGoWriter
    {
        [GoMethod]
        Slice<byte> Sum(Slice<byte> b);
        [GoMethod]
        void Reset();
        [GoMethod]
        long Size();
        [GoMethod]
        long BlockSize();
    }
}
