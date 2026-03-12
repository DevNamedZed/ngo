using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Archive.Tar
{
    // tar.Reader struct
    [GoType("struct", Name = "Reader", Package = "archive/tar")]
    public class GoReader
    {
        // Next advances to the next entry in the tar archive
        [GoMethod]
        [return: GoReturn("*tar.Header", "error")]
        public (GoHeader?, object?) Next() => (null, null);

        // Read reads from the current file in the tar archive
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> b) => (0, null);
    }
}
