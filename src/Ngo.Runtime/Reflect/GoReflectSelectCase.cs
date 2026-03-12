using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Reflect
{
    [GoType("struct", Name = "SelectCase", Package = "reflect")]
    public struct GoReflectSelectCase
    {
        [GoField(Name = "Dir")]
        public long Dir;

        [GoField(Name = "Chan")]
        public GoReflectValue Chan;

        [GoField(Name = "Send")]
        public GoReflectValue Send;
    }
}
