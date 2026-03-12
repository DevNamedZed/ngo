using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Unsafe
{
    [GoType("struct", Name = "Pointer", Package = "unsafe")]
    public class GoUnsafePointer
    {
        public object? Value;

        public GoUnsafePointer() { }
        public GoUnsafePointer(object? value) { Value = value; }
    }
}
