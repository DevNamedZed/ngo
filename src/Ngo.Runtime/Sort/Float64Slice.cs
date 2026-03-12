using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sort
{
    /// <summary>
    /// sort.Float64Slice — named type []float64 implementing sort.Interface.
    /// </summary>
    [GoType("named", Name = "Float64Slice", Package = "sort", Underlying = "[]float64")]
    public struct Float64Slice : Interface
    {
        private Slice<double> _data;

        public Float64Slice(Slice<double> data) { _data = data; }

        public static implicit operator Float64Slice(Slice<double> s) => new Float64Slice(s);
        public static implicit operator Slice<double>(Float64Slice s) => s._data;

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
        public long Search(double x)
        {
            int lo = 0, hi = _data.Len;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (_data[mid] < x)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return (long)lo;
        }
    }
}
