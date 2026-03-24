using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Syscall
{
    [GoType("struct", Name = "EpollEvent", Package = "syscall")]
    public struct GoEpollEvent
    {
        [GoField] public long Events;
        [GoField] public long Fd;
        [GoField] public long Pad;
    }
}
