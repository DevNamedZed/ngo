using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "Pool", Package = "sync")]
    public sealed class Pool
    {
        [GoField(Name = "New")]
        public Func<object?>? New;

        private readonly System.Collections.Concurrent.ConcurrentBag<object?> _pool = new();

        [GoMethod]
        public object? Get()
        {
            if (_pool.TryTake(out var item))
                return item;
            if (New != null)
                return New();
            return null;
        }

        [GoMethod]
        public void Put(object? x)
        {
            _pool.Add(x);
        }
    }
}
