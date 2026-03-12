using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoPackage("sync")]
    public static class Package
    {
        public static Cond NewCond(object? l) => Cond.NewCond(l);
    }
}
