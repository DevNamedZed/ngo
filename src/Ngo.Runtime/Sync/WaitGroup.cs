using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "WaitGroup", Package = "sync")]
    public sealed class WaitGroup
    {
        private int _counter;
        private readonly ManualResetEventSlim _event = new(true);

        [GoMethod]
        public void Add([GoParam("int")] long delta)
        {
            int newVal = Interlocked.Add(ref _counter, (int)delta);
            if (newVal < 0)
            {
                throw new GoPanicException("sync: negative WaitGroup counter");
            }
            if (newVal == 0)
            {
                _event.Set();
            }
            else
            {
                _event.Reset();
            }
        }

        [GoMethod]
        public void Done()
        {
            Add(-1);
        }

        [GoMethod]
        public void Wait()
        {
            _event.Wait();
        }
    }
}
