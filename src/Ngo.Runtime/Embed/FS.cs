using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Embed
{
    [GoType("struct", Name = "FS", Package = "embed")]
    public class FS
    {
        [GoMethod]
        public (Slice<byte>, object?) ReadFile(string name)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(name);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoMethod]
        public (Slice<DirEntry>, object?) ReadDir(string name)
        {
            return (new Slice<DirEntry>(Array.Empty<DirEntry>()), null);
        }
    }
}
