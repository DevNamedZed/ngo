using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    [GoType("struct", Name = "MemProfileRecord", Package = "runtime")]
    public class GoMemProfileRecord
    {
        [GoField(Name = "AllocBytes")]
        public long AllocBytes;

        [GoField(Name = "FreeBytes")]
        public long FreeBytes;

        [GoField(Name = "AllocObjects")]
        public long AllocObjects;

        [GoField(Name = "FreeObjects")]
        public long FreeObjects;

        [GoField(Name = "Stack0", Type = "[32]uintptr")]
        public Slice<long> Stack0 = new Slice<long>(new long[32]);

        [GoMethod]
        public long InUseBytes() => AllocBytes - FreeBytes;

        [GoMethod]
        public long InUseObjects() => AllocObjects - FreeObjects;

        [GoMethod]
        public Slice<long> Stack() => Stack0;
    }
}
