using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    /// <summary>
    /// Go runtime.Frames — iterator over stack frames.
    /// </summary>
    [GoType("struct", Name = "Frames", Package = "runtime")]
    public sealed class GoRuntimeFrames
    {
        private readonly Slice<long> _callers;
        private int _index;

        internal GoRuntimeFrames(Slice<long> callers)
        {
            _callers = callers;
            _index = 0;
        }

        [GoMethod]
        public (GoRuntimeFrame, bool) Next()
        {
            if (_index >= _callers.Len)
                return (new GoRuntimeFrame(), false);
            var frame = new GoRuntimeFrame { PC = _callers[_index] };
            _index++;
            return (frame, _index < _callers.Len);
        }
    }
}
