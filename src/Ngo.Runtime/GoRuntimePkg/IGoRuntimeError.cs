using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    /// <summary>
    /// Go runtime.Error interface — extends error with RuntimeError() method.
    /// </summary>
    [GoType("interface", Name = "Error", Package = "runtime")]
    public interface IGoRuntimeError
    {
        string Error();
        void RuntimeError();
    }
}
