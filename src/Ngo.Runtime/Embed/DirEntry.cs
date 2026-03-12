using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Embed
{
    [GoType("struct", Name = "DirEntry", Package = "embed")]
    public class DirEntry
    {
        [GoMethod]
        public string Name() => "";

        [GoMethod]
        public bool IsDir() => false;
    }
}
