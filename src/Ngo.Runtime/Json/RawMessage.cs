using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    [GoType("named", Name = "RawMessage", Underlying = "[]byte", Package = "encoding/json")]
    public struct RawMessage
    {
        public Slice<byte> Value;

        public RawMessage(Slice<byte> value) { Value = value; }

        public static implicit operator Slice<byte>(RawMessage rm) => rm.Value;
        public static implicit operator RawMessage(Slice<byte> s) => new RawMessage(s);
    }
}
