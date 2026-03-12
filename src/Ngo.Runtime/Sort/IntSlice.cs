using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sort
{
    /// <summary>
    /// sort.IntSlice — named type []int implementing sort.Interface.
    /// </summary>
    [GoType("named", Name = "IntSlice", Package = "sort", Underlying = "[]int")]
    public struct IntSlice : Interface
    {
        private Slice<long> _data;

        public IntSlice(Slice<long> data) { _data = data; }

        public static implicit operator IntSlice(Slice<long> s) => new IntSlice(s);
        public static implicit operator Slice<long>(IntSlice s) => s._data;

        [GoMethod]
        public long Len() => _data.Len;

        [GoMethod]
        public bool Less(long i, long j) => _data[(int)i] < _data[(int)j];

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
        public long Search(long x) => Package.SearchInts(_data, x);
    }
}
