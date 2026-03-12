using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoType("named", Name = "Values", Package = "net/url", Underlying = "map[string][]string")]
    public class GoValues
    {
        private readonly System.Collections.Generic.Dictionary<string, Slice<string>> _values = new();

        [GoMethod]
        public string Get(string key)
        {
            if (_values.TryGetValue(key, out var vals) && vals.Len > 0)
                return vals[0];
            return "";
        }

        [GoMethod]
        public void Set(string key, string value)
        {
            _values[key] = new Slice<string>(new[] { value });
        }

        [GoMethod]
        public void Add(string key, string value)
        {
            if (_values.TryGetValue(key, out var vals))
            {
                var arr = new string[vals.Len + 1];
                for (int i = 0; i < vals.Len; i++) arr[i] = vals[i];
                arr[vals.Len] = value;
                _values[key] = new Slice<string>(arr);
            }
            else
            {
                _values[key] = new Slice<string>(new[] { value });
            }
        }

        [GoMethod]
        public void Del(string key) => _values.Remove(key);

        [GoMethod]
        public bool Has(string key) => _values.ContainsKey(key);

        [GoMethod]
        public string Encode() => "";
    }
}
