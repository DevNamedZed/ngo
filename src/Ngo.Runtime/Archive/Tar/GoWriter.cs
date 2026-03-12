using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Archive.Tar
{
    // tar.Writer struct
    [GoType("struct", Name = "Writer", Package = "archive/tar")]
    public class GoWriter
    {
        // WriteHeader writes hdr and prepares to accept the file's contents
        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteHeader([GoParam("*tar.Header")] GoHeader? hdr) => null;

        // Write writes to the current file in the tar archive
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> b) => (0, null);

        // Close closes the tar archive
        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        // Flush finishes writing the current file's block padding
        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => null;
    }
}
