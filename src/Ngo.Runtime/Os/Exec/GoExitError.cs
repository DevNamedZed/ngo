using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.Exec
{
    [GoType("struct", Name = "ExitError", Package = "os/exec")]
    public class GoExitError
    {
        [GoField]
        public Slice<byte> Stderr;

        [GoMethod]
        public string Error() => "exit status non-zero";

        [GoMethod]
        public long ExitCode() => 1;

        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? Sys() => null;
    }
}
