using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sort
{
    /// <summary>
    /// sort.Interface — the interface that sorting algorithms use.
    /// </summary>
    [GoType("interface", Name = "Interface", Package = "sort")]
    public interface Interface
    {
        [GoMethod]
        long Len();
        [GoMethod]
        bool Less(long i, long j);
        [GoMethod]
        void Swap(long i, long j);
    }
}
