using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os
{
    [GoType("named", Name = "FileMode", Underlying = "uint32", Package = "os")]
    public struct GoFileMode
    {
        public long Value;

        public GoFileMode(long value) { Value = value; }

        [GoMethod]
        public bool IsDir() => (Value & (1 << 31)) != 0;

        [GoMethod]
        public bool IsRegular() => (Value & (1 << 31 | 1 << 27)) == 0;

        [GoMethod]
        public long Perm() => Value & 0777;

        [GoMethod]
        public string String()
        {
            return $"0{System.Convert.ToString(Value & 0777, 8).PadLeft(3, '0')}";
        }

        public static implicit operator long(GoFileMode m) => m.Value;
        public static implicit operator GoFileMode(long v) => new GoFileMode(v);

        public override string ToString() => String();
    }
}
