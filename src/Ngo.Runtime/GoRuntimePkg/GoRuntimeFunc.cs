namespace Ngo.Runtime.GoRuntimePkg
{
    /// <summary>
    /// Go runtime.Func — wraps .NET stack frame info.
    /// </summary>
    public sealed class GoRuntimeFunc
    {
        private readonly long _pc;

        internal GoRuntimeFunc(long pc)
        {
            _pc = pc;
        }

        public string Name()
        {
            // Best-effort: walk the stack to find a matching frame
            return "unknown";
        }

        public long Entry()
        {
            return _pc;
        }

        public (string file, long line) FileLine(long pc)
        {
            return ("unknown", 0);
        }
    }
}
