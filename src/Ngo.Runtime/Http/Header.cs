using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("named", Name = "Header", Package = "net/http", Underlying = "map[string][]string")]
    public class Header
    {
        internal readonly Map<string, Slice<string>> _values = new Map<string, Slice<string>>();

        [GoMethod]
        public string Get(string key)
        {
            var (v, ok) = _values.Get(key);
            if (ok && v.Len > 0) return v[0];
            return "";
        }

        [GoMethod]
        public void Set(string key, string value)
        {
            _values.Set(key, new Slice<string>(new[] { value }));
        }

        [GoMethod]
        public void Add(string key, string value)
        {
            var (existing, _) = _values.Get(key);
            var list = new List<string>();
            for (int i = 0; i < existing.Len; i++) list.Add(existing[i]);
            list.Add(value);
            _values.Set(key, new Slice<string>(list.ToArray()));
        }

        [GoMethod]
        public void Del(string key) { _values.Delete(key); }

        [GoMethod]
        public Slice<string> Values(string key) { var (v, _) = _values.Get(key); return v; }

        [GoMethod]
        public Header Clone() => new Header();

        [GoMethod]
        [return: GoReturn("error")]
        public object? Write(object? w) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteSubset(object? w, object? exclude) => null;
    }
}
