using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoPackage("sync/atomic")]
    public static class Package
    {
        public static long AddInt32(Ptr<long> addr, long delta)
        {
            addr.Value += delta;
            return addr.Value;
        }

        public static long AddInt64(Ptr<long> addr, long delta)
        {
            addr.Value += delta;
            return addr.Value;
        }

        public static long LoadInt32(Ptr<long> addr)
        {
            return addr.Value;
        }

        public static long LoadInt64(Ptr<long> addr)
        {
            return addr.Value;
        }

        public static void StoreInt32(Ptr<long> addr, long val)
        {
            addr.Value = val;
        }

        public static void StoreInt64(Ptr<long> addr, long val)
        {
            addr.Value = val;
        }

        public static bool CompareAndSwapInt32(Ptr<long> addr, long old, long @new)
        {
            if (addr.Value == old) { addr.Value = @new; return true; }
            return false;
        }

        public static bool CompareAndSwapInt64(Ptr<long> addr, long old, long @new)
        {
            if (addr.Value == old) { addr.Value = @new; return true; }
            return false;
        }

        public static long LoadUint32(Ptr<long> addr) => addr.Value;
        public static long LoadUint64(Ptr<long> addr) => addr.Value;
        public static void StoreUint32(Ptr<long> addr, long val) { addr.Value = val; }
        public static void StoreUint64(Ptr<long> addr, long val) { addr.Value = val; }
        public static long AddUint32(Ptr<long> addr, long delta) { addr.Value += delta; return addr.Value; }
        public static long AddUint64(Ptr<long> addr, long delta) { addr.Value += delta; return addr.Value; }
        public static bool CompareAndSwapUint32(Ptr<long> addr, long old, long @new)
        {
            if (addr.Value == old) { addr.Value = @new; return true; }
            return false;
        }
        public static bool CompareAndSwapUint64(Ptr<long> addr, long old, long @new)
        {
            if (addr.Value == old) { addr.Value = @new; return true; }
            return false;
        }
        public static long SwapInt32(Ptr<long> addr, long @new) { var old = addr.Value; addr.Value = @new; return old; }
        public static long SwapInt64(Ptr<long> addr, long @new) { var old = addr.Value; addr.Value = @new; return old; }
        public static long SwapUint32(Ptr<long> addr, long @new) { var old = addr.Value; addr.Value = @new; return old; }
        public static long SwapUint64(Ptr<long> addr, long @new) { var old = addr.Value; addr.Value = @new; return old; }
        [GoFunc]
        [return: GoReturn("unsafe.Pointer")]
        public static long LoadPointer([GoParam("*unsafe.Pointer")] Ptr<long> addr) => addr.Value;

        [GoFunc]
        public static void StorePointer([GoParam("*unsafe.Pointer")] Ptr<long> addr, [GoParam("unsafe.Pointer")] long val) { addr.Value = val; }

        [GoFunc]
        [return: GoReturn("unsafe.Pointer")]
        public static long SwapPointer([GoParam("*unsafe.Pointer")] Ptr<long> addr, [GoParam("unsafe.Pointer")] long @new) { var old = addr.Value; addr.Value = @new; return old; }

        public static long LoadUintptr(Ptr<long> addr) => addr.Value;
        public static void StoreUintptr(Ptr<long> addr, long val) { addr.Value = val; }
        public static long AddUintptr(Ptr<long> addr, long delta) { addr.Value += delta; return addr.Value; }
        public static bool CompareAndSwapUintptr(Ptr<long> addr, long old, long @new)
        {
            if (addr.Value == old) { addr.Value = @new; return true; }
            return false;
        }

        [GoFunc]
        public static bool CompareAndSwapPointer([GoParam("*unsafe.Pointer")] Ptr<long> addr, [GoParam("unsafe.Pointer")] long old, [GoParam("unsafe.Pointer")] long @new)
        {
            if (addr.Value == old) { addr.Value = @new; return true; }
            return false;
        }
    }
}
