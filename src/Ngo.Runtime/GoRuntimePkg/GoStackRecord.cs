using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    [GoType("struct", Name = "StackRecord", Package = "runtime")]
    public class GoStackRecord
    {
        [GoField(Name = "Stack0", Type = "[32]uintptr")]
        public Slice<long> Stack0 = new Slice<long>(new long[32]);

        [GoMethod]
        public Slice<long> Stack() => Stack0;
    }
}
