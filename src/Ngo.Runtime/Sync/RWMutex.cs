using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "RWMutex", Package = "sync")]
    public sealed class RWMutex
    {
        private readonly ReaderWriterLockSlim _rwlock = new();

        [GoMethod]
        public void RLock() => _rwlock.EnterReadLock();

        [GoMethod]
        public void RUnlock() => _rwlock.ExitReadLock();

        [GoMethod]
        public void Lock() => _rwlock.EnterWriteLock();

        [GoMethod]
        public void Unlock() => _rwlock.ExitWriteLock();

        [GoMethod]
        public bool TryLock() => _rwlock.TryEnterWriteLock(0);

        [GoMethod]
        public bool TryRLock() => _rwlock.TryEnterReadLock(0);

        [GoMethod]
        public ILocker RLocker() => new RWMutexReadLocker(this);
    }

    internal sealed class RWMutexReadLocker : ILocker
    {
        private readonly RWMutex _mu;
        internal RWMutexReadLocker(RWMutex mu) { _mu = mu; }
        public void Lock() => _mu.RLock();
        public void Unlock() => _mu.RUnlock();
    }
}
