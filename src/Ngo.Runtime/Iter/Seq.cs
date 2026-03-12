using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Iter
{
    [GoType("named", Name = "Seq", Underlying = "func(func(V) bool)", Package = "iter")]
    public struct GoIterSeq<V>
    {
        public Action<Func<V, bool>> Value;

        public GoIterSeq(Action<Func<V, bool>> value) { Value = value; }
    }
}
