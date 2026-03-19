using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Token
{
    [GoPackage("go/token")]
    public static class Package
    {
        // Token constants (must match go/token/token.go iota values)
        [GoConst(Type = "token.Token")] public const long ILLEGAL = 0;
        [GoConst(Type = "token.Token")] public const long EOF = 1;
        [GoConst(Type = "token.Token")] public const long COMMENT = 2;

        [GoConst(Type = "token.Token")] public const long IDENT = 4;
        [GoConst(Type = "token.Token")] public const long INT = 5;
        [GoConst(Type = "token.Token")] public const long FLOAT = 6;
        [GoConst(Type = "token.Token")] public const long IMAG = 7;
        [GoConst(Type = "token.Token")] public const long CHAR = 8;
        [GoConst(Type = "token.Token")] public const long STRING = 9;

        [GoConst(Type = "token.Token")] public const long ADD = 12;
        [GoConst(Type = "token.Token")] public const long SUB = 13;
        [GoConst(Type = "token.Token")] public const long MUL = 14;
        [GoConst(Type = "token.Token")] public const long QUO = 15;
        [GoConst(Type = "token.Token")] public const long REM = 16;

        [GoConst(Type = "token.Token")] public const long AND = 17;
        [GoConst(Type = "token.Token")] public const long OR = 18;
        [GoConst(Type = "token.Token")] public const long XOR = 19;
        [GoConst(Type = "token.Token")] public const long SHL = 20;
        [GoConst(Type = "token.Token")] public const long SHR = 21;
        [GoConst(Type = "token.Token")] public const long AND_NOT = 22;

        [GoConst(Type = "token.Token")] public const long ADD_ASSIGN = 23;
        [GoConst(Type = "token.Token")] public const long SUB_ASSIGN = 24;
        [GoConst(Type = "token.Token")] public const long MUL_ASSIGN = 25;
        [GoConst(Type = "token.Token")] public const long QUO_ASSIGN = 26;
        [GoConst(Type = "token.Token")] public const long REM_ASSIGN = 27;

        [GoConst(Type = "token.Token")] public const long AND_ASSIGN = 28;
        [GoConst(Type = "token.Token")] public const long OR_ASSIGN = 29;
        [GoConst(Type = "token.Token")] public const long XOR_ASSIGN = 30;
        [GoConst(Type = "token.Token")] public const long SHL_ASSIGN = 31;
        [GoConst(Type = "token.Token")] public const long SHR_ASSIGN = 32;
        [GoConst(Type = "token.Token")] public const long AND_NOT_ASSIGN = 33;

        [GoConst(Type = "token.Token")] public const long LAND = 34;
        [GoConst(Type = "token.Token")] public const long LOR = 35;
        [GoConst(Type = "token.Token")] public const long ARROW = 36;
        [GoConst(Type = "token.Token")] public const long INC = 37;
        [GoConst(Type = "token.Token")] public const long DEC = 38;

        [GoConst(Type = "token.Token")] public const long EQL = 39;
        [GoConst(Type = "token.Token")] public const long LSS = 40;
        [GoConst(Type = "token.Token")] public const long GTR = 41;
        [GoConst(Type = "token.Token")] public const long ASSIGN = 42;
        [GoConst(Type = "token.Token")] public const long NOT = 43;

        [GoConst(Type = "token.Token")] public const long NEQ = 44;
        [GoConst(Type = "token.Token")] public const long LEQ = 45;
        [GoConst(Type = "token.Token")] public const long GEQ = 46;
        [GoConst(Type = "token.Token")] public const long DEFINE = 47;
        [GoConst(Type = "token.Token")] public const long ELLIPSIS = 48;
        [GoConst(Type = "token.Token")] public const long TILDE = 49;

        [GoConst(Type = "token.Token")] public const long LPAREN = 50;
        [GoConst(Type = "token.Token")] public const long LBRACK = 51;
        [GoConst(Type = "token.Token")] public const long LBRACE = 52;
        [GoConst(Type = "token.Token")] public const long COMMA = 53;
        [GoConst(Type = "token.Token")] public const long PERIOD = 54;

        [GoConst(Type = "token.Token")] public const long RPAREN = 55;
        [GoConst(Type = "token.Token")] public const long RBRACK = 56;
        [GoConst(Type = "token.Token")] public const long RBRACE = 57;
        [GoConst(Type = "token.Token")] public const long SEMICOLON = 58;
        [GoConst(Type = "token.Token")] public const long COLON = 59;

        [GoConst(Type = "token.Token")] public const long BREAK = 61;
        [GoConst(Type = "token.Token")] public const long CASE = 62;
        [GoConst(Type = "token.Token")] public const long CHAN = 63;
        [GoConst(Type = "token.Token")] public const long CONST = 64;
        [GoConst(Type = "token.Token")] public const long CONTINUE = 65;

        [GoConst(Type = "token.Token")] public const long DEFAULT = 66;
        [GoConst(Type = "token.Token")] public const long DEFER = 67;
        [GoConst(Type = "token.Token")] public const long ELSE = 68;
        [GoConst(Type = "token.Token")] public const long FALLTHROUGH = 69;
        [GoConst(Type = "token.Token")] public const long FOR = 70;

        [GoConst(Type = "token.Token")] public const long FUNC = 71;
        [GoConst(Type = "token.Token")] public const long GO = 72;
        [GoConst(Type = "token.Token")] public const long GOTO = 73;
        [GoConst(Type = "token.Token")] public const long IF = 74;
        [GoConst(Type = "token.Token")] public const long IMPORT = 75;

        [GoConst(Type = "token.Token")] public const long INTERFACE = 76;
        [GoConst(Type = "token.Token")] public const long MAP = 77;
        [GoConst(Type = "token.Token")] public const long PACKAGE = 78;
        [GoConst(Type = "token.Token")] public const long RANGE = 79;
        [GoConst(Type = "token.Token")] public const long RETURN = 80;

        [GoConst(Type = "token.Token")] public const long SELECT = 81;
        [GoConst(Type = "token.Token")] public const long STRUCT = 82;
        [GoConst(Type = "token.Token")] public const long SWITCH = 83;
        [GoConst(Type = "token.Token")] public const long TYPE = 84;
        [GoConst(Type = "token.Token")] public const long VAR = 85;

        // Highest precedence / count
        [GoConst(Type = "int")] public const long LowestPrec = 0;
        [GoConst(Type = "int")] public const long UnaryPrec = 6;
        [GoConst(Type = "int")] public const long HighestPrec = 7;

        // NoPos sentinel
        [GoConst(Type = "token.Pos")] public const long NoPos = 0;

        // token.Lookup(ident string) Token
        [GoFunc]
        [return: GoReturn("token.Token")]
        public static long Lookup(string ident)
        {
            return ident switch
            {
                "break" => BREAK, "case" => CASE, "chan" => CHAN, "const" => CONST,
                "continue" => CONTINUE, "default" => DEFAULT, "defer" => DEFER,
                "else" => ELSE, "fallthrough" => FALLTHROUGH, "for" => FOR,
                "func" => FUNC, "go" => GO, "goto" => GOTO, "if" => IF,
                "import" => IMPORT, "interface" => INTERFACE, "map" => MAP,
                "package" => PACKAGE, "range" => RANGE, "return" => RETURN,
                "select" => SELECT, "struct" => STRUCT, "switch" => SWITCH,
                "type" => TYPE, "var" => VAR,
                _ => IDENT,
            };
        }

        // token.NewFileSet() *FileSet
        [GoFunc]
        [return: GoReturn("*token.FileSet")]
        public static GoFileSet NewFileSet() => new GoFileSet();

        // token.IsExported(name string) bool — Go 1.21+
        [GoFunc]
        public static bool IsExported(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            return char.IsUpper(name[0]);
        }

        // token.IsIdentifier(name string) bool — Go 1.21+
        [GoFunc]
        public static bool IsIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            if (!char.IsLetter(name[0]) && name[0] != '_')
            {
                return false;
            }
            for (int i = 1; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                {
                    return false;
                }
            }
            return true;
        }

        // token.IsKeyword(name string) bool — Go 1.21+
        [GoFunc]
        public static bool IsKeyword(string name)
        {
            return name switch
            {
                "break" or "case" or "chan" or "const" or "continue" or
                "default" or "defer" or "else" or "fallthrough" or "for" or
                "func" or "go" or "goto" or "if" or "import" or
                "interface" or "map" or "package" or "range" or "return" or
                "select" or "struct" or "switch" or "type" or "var" => true,
                _ => false,
            };
        }
    }

    // token.Token type
    [GoType("named", Name = "Token", Package = "go/token", Underlying = "int")]
    public struct GoTokenType
    {
        [GoMethod] public bool IsLiteral() => false;
        [GoMethod] public bool IsOperator() => false;
        [GoMethod] public bool IsKeyword() => false;
        [GoMethod] public long Precedence() => 0;
        [GoMethod] public string String() => "";
    }

    // token.Pos type
    [GoType("named", Name = "Pos", Package = "go/token", Underlying = "int")]
    public struct GoPosType
    {
        [GoMethod] public bool IsValid() => false;
    }

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
        public string String()
        {
            string s = Filename;
            if (IsValid())
            {
                if (!string.IsNullOrEmpty(s))
                {
                    s += ":";
                }
                s += Line.ToString();
                if (Column > 0)
                {
                    s += ":" + Column.ToString();
                }
            }
            return s;
        }

        [GoMethod]
        public override string ToString() => String();
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

        [GoMethod]
        [return: GoReturn("token.Position")]
        public GoPosition PositionFor(long pos, bool adjusted) => new GoPosition();

        [GoMethod]
        [return: GoReturn("*token.File")]
        public GoFile? File(long pos) => null;

        [GoMethod]
        public void Iterate(object? f) { }

        [GoMethod]
        [return: GoReturn("int")]
        public long Base() => 1;
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

        private readonly System.Collections.Generic.List<int> _lines = new() { 0 };

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
        public GoPosition Position(long pos)
        {
            int offset = (int)pos;
            int line = 1;
            int col = offset + 1;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i] <= offset)
                {
                    line = i + 1;
                    col = offset - _lines[i] + 1;
                    break;
                }
            }
            return new GoPosition { Filename = _name, Line = line, Column = col, Offset = offset };
        }

        [GoMethod]
        public long LineCount() => _lines.Count;

        [GoMethod]
        public void AddLine(long offset)
        {
            _lines.Add((int)offset);
        }

        [GoMethod]
        public void AddLineColumnInfo(long offset, string filename, long line, long column)
        {
            // Store line info at offset — simplified implementation
            AddLine(offset);
        }

        [GoMethod]
        public long Line(long pos)
        {
            return Position(pos).Line;
        }

        [GoMethod]
        public long LineStart(long line)
        {
            if (line >= 1 && line <= _lines.Count)
            {
                return _lines[(int)line - 1];
            }
            return 0;
        }

        [GoMethod]
        [return: GoReturn("token.Pos")]
        public long Base()
        {
            return 0;
        }

        [GoMethod]
        public void MergeLine([GoParam("int")] long line) { }

        [GoMethod]
        [return: GoReturn("token.Position")]
        public GoPosition PositionFor(long pos, bool adjusted) => Position(pos);

        [GoMethod]
        public void SetLinesForContent(Slice<byte> content) { }
    }
}
