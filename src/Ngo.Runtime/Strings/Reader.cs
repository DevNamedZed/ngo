using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Strings
{
    [GoType("struct", Name = "Reader", Package = "strings")]
    public sealed class Reader
    {
        private string _s;
        private int _i;

        public Reader(string s) { _s = s; _i = 0; }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read([GoParam("[]byte")] Slice<byte> p)
        {
            if (_i >= _s.Length) return (0, "EOF");
            int n = global::System.Math.Min(p.Len, _s.Length - _i);
            for (int j = 0; j < n; j++)
                p[j] = (byte)_s[_i + j];
            _i += n;
            return (n, null);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadAt([GoParam("[]byte")] Slice<byte> p, long off)
        {
            if (off >= _s.Length) return (0, "EOF");
            int n = global::System.Math.Min(p.Len, _s.Length - (int)off);
            for (int j = 0; j < n; j++)
                p[j] = (byte)_s[(int)off + j];
            return (n, n < p.Len ? (object)"EOF" : null);
        }

        [GoMethod]
        [return: GoReturn("byte", "error")]
        public (byte, object?) ReadByte()
        {
            if (_i >= _s.Length) return (0, "EOF");
            return ((byte)_s[_i++], null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnreadByte()
        {
            if (_i <= 0) return "strings.Reader.UnreadByte: at beginning of string";
            _i--;
            return null;
        }

        [GoMethod]
        [return: GoReturn("rune", "int", "error")]
        public (long, long, object?) ReadRune()
        {
            if (_i >= _s.Length) return (0, 0, "EOF");
            var c = _s[_i];
            _i++;
            return ((long)c, 1, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnreadRune()
        {
            if (_i <= 0) return "strings.Reader.UnreadRune: at beginning of string";
            _i--;
            return null;
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Seek(long offset, long whence)
        {
            long abs;
            switch (whence)
            {
                case 0: abs = offset; break;
                case 1: abs = _i + offset; break;
                case 2: abs = _s.Length + offset; break;
                default: return (0, "strings.Reader.Seek: invalid whence");
            }
            if (abs < 0) return (0, "strings.Reader.Seek: negative position");
            _i = (int)abs;
            return (abs, null);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) WriteTo([GoParam("io.Writer")] object w)
        {
            return (_s.Length - _i, null);
        }

        [GoMethod]
        public long Len() => _s.Length - _i;

        [GoMethod]
        public long Size() => _s.Length;

        [GoMethod]
        public void Reset(string s) { _s = s; _i = 0; }
    }
}
