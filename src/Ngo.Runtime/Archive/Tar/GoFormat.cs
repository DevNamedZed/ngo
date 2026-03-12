using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Archive.Tar
{
    // tar.Format named type
    [GoType("named", Name = "Format", Package = "archive/tar", Underlying = "int")]
    public struct GoFormat
    {
        public long Value;

        [GoMethod]
        public string String() => "";
    }
}
