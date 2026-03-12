using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    [GoType("struct", Name = "BlockProfileRecord", Package = "runtime")]
    public class GoBlockProfileRecord
    {
        [GoField(Name = "Count")]
        public long Count;

        [GoField(Name = "Cycles")]
        public long Cycles;

        // Embedded StackRecord
        [GoField]
        public GoStackRecord StackRecord = new GoStackRecord();

        [GoMethod]
        public Slice<long> Stack() => StackRecord.Stack();
    }
}
