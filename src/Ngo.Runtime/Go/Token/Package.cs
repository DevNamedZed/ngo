using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Token
{
    [GoPackage("go/token")]
    public static class Package
    {
        // token.NewFileSet() *FileSet
        [GoFunc]
        [return: GoReturn("*token.FileSet")]
        public static GoFileSet NewFileSet() => new GoFileSet();
    }

    // token.Token type
    [GoType("named", Name = "Token", Package = "go/token", Underlying = "int")]
    public struct GoTokenType { }

    // token.Pos type
    [GoType("named", Name = "Pos", Package = "go/token", Underlying = "int")]
    public struct GoPosType { }

    // token.Position struct
    [GoType("struct", Name = "Position", Package = "go/token")]
    public class GoPosition
    {
        [GoField(Name = "Filename")]
        public string Filename = "";

        [GoField(Name = "Offset")]
        public long Offset;

        [GoField(Name = "Line")]
        public long Line;

        [GoField(Name = "Column")]
        public long Column;

        [GoMethod]
        public bool IsValid() => Line > 0;

        [GoMethod]
        public override string ToString() => $"{Filename}:{Line}:{Column}";
    }

    // token.FileSet struct
    [GoType("struct", Name = "FileSet", Package = "go/token")]
    public class GoFileSet
    {
        [GoMethod]
        [return: GoReturn("*token.File")]
        public GoFile AddFile(string filename, long @base, long size) => new GoFile(filename, size);

        [GoMethod]
        [return: GoReturn("token.Position")]
        public GoPosition Position(long pos) => new GoPosition();
    }

    // token.File struct
    [GoType("struct", Name = "File", Package = "go/token")]
    public class GoFile
    {
        private readonly string _name;
        private readonly long _size;

        public GoFile(string name, long size)
        {
            _name = name;
            _size = size;
        }

        [GoMethod]
        public string Name() => _name;

        [GoMethod]
        public long Size() => _size;

        [GoMethod]
        [return: GoReturn("token.Pos")]
        public long Pos(long offset) => offset;

        [GoMethod]
        public long Offset(long pos) => pos;

        [GoMethod]
        [return: GoReturn("token.Position")]
        public GoPosition Position(long pos) => new GoPosition { Filename = _name, Line = 1, Column = 1 };

        [GoMethod]
        public long LineCount() => 1;
    }
}
