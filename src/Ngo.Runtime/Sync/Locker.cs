using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    // sync.Locker interface
    [GoType("interface", Name = "Locker", Package = "sync")]
    public interface ILocker
    {
        [GoMethod]
        void Lock();
        [GoMethod]
        void Unlock();
    }
}
