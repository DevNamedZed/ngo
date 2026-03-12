using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.Exec
{
    [GoType("struct", Package = "os/exec", Name = "Error")]
    public class GoExecError
    {
        [GoField]
        public string Name;

        [GoField]
        public object Err;

        [GoMethod]
        public string Error() => $"exec: {Name}: {Err}";
    }
}
