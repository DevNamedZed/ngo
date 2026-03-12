using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sort
{
    /// <summary>
    /// sort.StringSlice — named type []string implementing sort.Interface.
    /// </summary>
    [GoType("named", Name = "StringSlice", Package = "sort", Underlying = "[]string")]
    public struct StringSlice : Interface
    {
        private Slice<string> _data;

        public StringSlice(Slice<string> data) { _data = data; }

        public static implicit operator StringSlice(Slice<string> s) => new StringSlice(s);
        public static implicit operator Slice<string>(StringSlice s) => s._data;

        [GoMethod]
        public long Len() => _data.Len;

        [GoMethod]
        public bool Less(long i, long j) =>
            string.Compare(_data[(int)i], _data[(int)j], StringComparison.Ordinal) < 0;

        [GoMethod]
        public void Swap(long i, long j)
        {
            var tmp = _data[(int)i];
            _data[(int)i] = _data[(int)j];
            _data[(int)j] = tmp;
        }

        [GoMethod]
        public void Sort() => Package.Sort(this);

        [GoMethod]
        public long Search(string x) => Package.SearchStrings(_data, x);
    }
}
