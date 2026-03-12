using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    /// <summary>
    /// Go runtime.Frame — information about a single stack frame.
    /// </summary>
    [GoType("struct", Name = "Frame", Package = "runtime")]
    public sealed class GoRuntimeFrame
    {
        [GoField(Name = "PC")]
        public long PC { get; set; }
        [GoField(Name = "Func")]
        public GoRuntimeFunc? Func { get; set; }
        [GoField(Name = "Function")]
        public string Function { get; set; } = "";
        [GoField(Name = "File")]
        public string File { get; set; } = "";
        [GoField(Name = "Line")]
        public long Line { get; set; }
        [GoField(Name = "Entry")]
        public long Entry { get; set; }
    }
}
