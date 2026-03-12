using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "Cond", Package = "sync")]
    public sealed class Cond
    {
        [GoField(Name = "L")]
        public object? L;

        private readonly object _lock = new();

        public static Cond NewCond(object? l)
        {
            return new Cond { L = l };
        }

        [GoMethod]
        public void Wait()
        {
            lock (_lock) { System.Threading.Monitor.Wait(_lock); }
        }

        [GoMethod]
        public void Signal()
        {
            lock (_lock) { System.Threading.Monitor.Pulse(_lock); }
        }

        [GoMethod]
        public void Broadcast()
        {
            lock (_lock) { System.Threading.Monitor.PulseAll(_lock); }
        }
    }
}
