using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Iter
{
    [GoType("named", Name = "Seq2", Underlying = "func(func(K, V) bool)", Package = "iter")]
    public struct GoIterSeq2<K, V>
    {
        public Action<Func<K, V, bool>> Value;

        public GoIterSeq2(Action<Func<K, V, bool>> value) { Value = value; }
    }
}
