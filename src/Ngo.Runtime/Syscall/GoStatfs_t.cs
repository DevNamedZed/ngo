using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Syscall
{
    [GoType("struct", Name = "Statfs_t", Package = "syscall")]
    public struct GoStatfs_t
    {
        [GoField] public long Type;
        [GoField] public long Bsize;
        [GoField] public long Blocks;
        [GoField] public long Bfree;
        [GoField] public long Bavail;
        [GoField] public long Files;
        [GoField] public long Ffree;
    }
}
