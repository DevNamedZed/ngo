using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    [GoType("struct", Name = "Func", Package = "runtime")]
    public sealed class GoRuntimeFunc
    {
        private readonly long _pc;

        internal GoRuntimeFunc(long pc)
        {
            _pc = pc;
        }

        [GoMethod]
        public string Name()
        {
            return "unknown";
        }

        [GoMethod]
        [return: GoReturn("uintptr")]
        public long Entry()
        {
            return _pc;
        }

        [GoMethod]
        [return: GoReturn("string", "int")]
        public (string file, long line) FileLine(long pc)
        {
            return ("unknown", 0);
        }
    }
}
