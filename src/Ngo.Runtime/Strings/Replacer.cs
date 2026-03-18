using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Strings
{
    [GoType("struct", Name = "Replacer", Package = "strings")]
    public sealed class Replacer
    {
        private readonly ReplacementPair[] _pairs;

        public Replacer(string[] pairs)
        {
            _pairs = new ReplacementPair[pairs.Length / 2];
            for (int i = 0; i < pairs.Length; i += 2)
            {
                _pairs[i / 2] = new ReplacementPair(pairs[i], pairs[i + 1]);
            }
        }

        [GoMethod]
        public string Replace(string s)
        {
            foreach (var pair in _pairs)
            {
                s = s.Replace(pair.OldValue, pair.NewValue);
            }

            return s;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteString([GoParam("io.Writer")] object w, string s)
        {
            var replaced = Replace(s);
            var bytes = System.Text.Encoding.UTF8.GetBytes(replaced);
            var slice = new Slice<byte>(bytes);
            if (w is Io.IGoWriter writer)
            {
                var (n, err) = writer.Write(slice);
                return (n, string.IsNullOrEmpty(err) ? null : err);
            }
            return (replaced.Length, null);
        }
    }
}
