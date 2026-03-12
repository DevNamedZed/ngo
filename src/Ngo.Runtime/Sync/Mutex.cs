using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "Mutex", Package = "sync")]
    public sealed class Mutex
    {
        private readonly object _lock = new();

        [GoMethod]
        public void Lock()
        {
            Monitor.Enter(_lock);
        }

        [GoMethod]
        public void Unlock()
        {
            Monitor.Exit(_lock);
        }

        [GoMethod]
        public bool TryLock()
        {
            return Monitor.TryEnter(_lock);
        }
    }
}
