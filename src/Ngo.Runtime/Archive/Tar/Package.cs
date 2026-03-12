using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Archive.Tar
{
    [GoPackage("archive/tar")]
    public static class Package
    {
        // Type flag constants
        [GoConst(Type = "byte")]
        public const byte TypeReg = (byte)'0';
        [GoConst(Type = "byte")]
        public const byte TypeLink = (byte)'1';
        [GoConst(Type = "byte")]
        public const byte TypeSymlink = (byte)'2';
        [GoConst(Type = "byte")]
        public const byte TypeChar = (byte)'3';
        [GoConst(Type = "byte")]
        public const byte TypeBlock = (byte)'4';
        [GoConst(Type = "byte")]
        public const byte TypeDir = (byte)'5';
        [GoConst(Type = "byte")]
        public const byte TypeFifo = (byte)'6';
        [GoConst(Type = "byte")]
        public const byte TypeXHeader = (byte)'x';
        [GoConst(Type = "byte")]
        public const byte TypeXGlobalHeader = (byte)'g';
        [GoConst(Type = "byte")]
        public const byte TypeGNULongName = (byte)'L';
        [GoConst(Type = "byte")]
        public const byte TypeGNULongLink = (byte)'K';
        [GoConst(Type = "byte")]
        public const byte TypeGNUSparse = (byte)'S';

        // Format constants
        [GoConst(Type = "tar.Format")]
        public const long FormatUnknown = 0;

        // Error variables
        [GoVar] public static readonly object? ErrHeader = "archive/tar: invalid tar header";
        [GoVar] public static readonly object? ErrWriteTooLong = "archive/tar: write too long";
        [GoVar] public static readonly object? ErrFieldTooLong = "archive/tar: header field too long";
        [GoVar] public static readonly object? ErrWriteAfterClose = "archive/tar: write after close";
        [GoVar] public static readonly object? ErrInsecurePath = "archive/tar: insecure file path";

        // tar.NewReader(r io.Reader) *Reader
        [GoFunc]
        [return: GoReturn("*tar.Reader")]
        public static GoReader NewReader([GoParam("io.Reader")] object? r) => new GoReader();

        // tar.NewWriter(w io.Writer) *Writer
        [GoFunc]
        [return: GoReturn("*tar.Writer")]
        public static GoWriter NewWriter([GoParam("io.Writer")] object? w) => new GoWriter();

        // tar.FileInfoHeader(fi fs.FileInfo, link string) (*Header, error)
        [GoFunc]
        [return: GoReturn("*tar.Header", "error")]
        public static (GoHeader?, object?) FileInfoHeader([GoParam("fs.FileInfo")] object? fi, string link) => (new GoHeader(), null);
    }
}
