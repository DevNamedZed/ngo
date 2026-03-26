using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoPackage("sync/atomic")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("int32")]
        public static long AddInt32(Ptr<long> addr, long delta)
        {
            return Interlocked.Add(ref addr.Value, delta);
        }

        [GoFunc]
        [return: GoReturn("int64")]
        public static long AddInt64(Ptr<long> addr, long delta)
        {
            return Interlocked.Add(ref addr.Value, delta);
        }

        [GoFunc]
        [return: GoReturn("int32")]
        public static long LoadInt32(Ptr<long> addr)
        {
            return Interlocked.Read(ref addr.Value);
        }

        [GoFunc]
        [return: GoReturn("int64")]
        public static long LoadInt64(Ptr<long> addr)
        {
            return Interlocked.Read(ref addr.Value);
        }

        [GoFunc]
        public static void StoreInt32(Ptr<long> addr, long val)
        {
            Interlocked.Exchange(ref addr.Value, val);
        }

        [GoFunc]
        public static void StoreInt64(Ptr<long> addr, long val)
        {
            Interlocked.Exchange(ref addr.Value, val);
        }

        [GoFunc]
        public static bool CompareAndSwapInt32(Ptr<long> addr, long old, long @new)
        {
            return Interlocked.CompareExchange(ref addr.Value, @new, old) == old;
        }

        [GoFunc]
        public static bool CompareAndSwapInt64(Ptr<long> addr, long old, long @new)
        {
            return Interlocked.CompareExchange(ref addr.Value, @new, old) == old;
        }

        [GoFunc]
        [return: GoReturn("uint32")]
        public static long LoadUint32(Ptr<long> addr)
        {
            return Interlocked.Read(ref addr.Value);
        }

        [GoFunc]
        [return: GoReturn("uint64")]
        public static long LoadUint64(Ptr<long> addr)
        {
            return Interlocked.Read(ref addr.Value);
        }

        [GoFunc]
        public static void StoreUint32(Ptr<long> addr, long val)
        {
            Interlocked.Exchange(ref addr.Value, val);
        }

        [GoFunc]
        public static void StoreUint64(Ptr<long> addr, long val)
        {
            Interlocked.Exchange(ref addr.Value, val);
        }

        [GoFunc]
        [return: GoReturn("uint32")]
        public static long AddUint32(Ptr<long> addr, long delta)
        {
            return Interlocked.Add(ref addr.Value, delta);
        }

        [GoFunc]
        [return: GoReturn("uint64")]
        public static long AddUint64(Ptr<long> addr, long delta)
        {
            return Interlocked.Add(ref addr.Value, delta);
        }

        [GoFunc]
        public static bool CompareAndSwapUint32(Ptr<long> addr, long old, long @new)
        {
            return Interlocked.CompareExchange(ref addr.Value, @new, old) == old;
        }

        [GoFunc]
        public static bool CompareAndSwapUint64(Ptr<long> addr, long old, long @new)
        {
            return Interlocked.CompareExchange(ref addr.Value, @new, old) == old;
        }

        [GoFunc]
        [return: GoReturn("int32")]
        public static long SwapInt32(Ptr<long> addr, long @new)
        {
            return Interlocked.Exchange(ref addr.Value, @new);
        }

        [GoFunc]
        [return: GoReturn("int64")]
        public static long SwapInt64(Ptr<long> addr, long @new)
        {
            return Interlocked.Exchange(ref addr.Value, @new);
        }

        [GoFunc]
        [return: GoReturn("uint32")]
        public static long SwapUint32(Ptr<long> addr, long @new)
        {
            return Interlocked.Exchange(ref addr.Value, @new);
        }

        [GoFunc]
        [return: GoReturn("uint64")]
        public static long SwapUint64(Ptr<long> addr, long @new)
        {
            return Interlocked.Exchange(ref addr.Value, @new);
        }

        [GoFunc]
        [return: GoReturn("unsafe.Pointer")]
        public static long LoadPointer([GoParam("*unsafe.Pointer")] Ptr<long> addr)
        {
            return Interlocked.Read(ref addr.Value);
        }

        [GoFunc]
        public static void StorePointer([GoParam("*unsafe.Pointer")] Ptr<long> addr, [GoParam("unsafe.Pointer")] long val)
        {
            Interlocked.Exchange(ref addr.Value, val);
        }

        [GoFunc]
        [return: GoReturn("unsafe.Pointer")]
        public static long SwapPointer([GoParam("*unsafe.Pointer")] Ptr<long> addr, [GoParam("unsafe.Pointer")] long @new)
        {
            return Interlocked.Exchange(ref addr.Value, @new);
        }

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long LoadUintptr(Ptr<long> addr)
        {
            return Interlocked.Read(ref addr.Value);
        }

        [GoFunc]
        public static void StoreUintptr(Ptr<long> addr, long val)
        {
            Interlocked.Exchange(ref addr.Value, val);
        }

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long AddUintptr(Ptr<long> addr, long delta)
        {
            return Interlocked.Add(ref addr.Value, delta);
        }

        [GoFunc]
        public static bool CompareAndSwapUintptr(Ptr<long> addr, long old, long @new)
        {
            return Interlocked.CompareExchange(ref addr.Value, @new, old) == old;
        }

        [GoFunc]
        public static bool CompareAndSwapPointer([GoParam("*unsafe.Pointer")] Ptr<long> addr, [GoParam("unsafe.Pointer")] long old, [GoParam("unsafe.Pointer")] long @new)
        {
            return Interlocked.CompareExchange(ref addr.Value, @new, old) == old;
        }

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long SwapUintptr(Ptr<long> addr, long @new)
        {
            return Interlocked.Exchange(ref addr.Value, @new);
        }
    }

    [GoType("struct", Name = "Uintptr", Package = "sync/atomic")]
    public class GoAtomicUintptr
    {
        private long _value;

        [GoMethod]
        public long Load() => Interlocked.Read(ref _value);

        [GoMethod]
        public void Store(long val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public long Add(long delta) => Interlocked.Add(ref _value, delta);

        [GoMethod]
        public long Swap(long val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public bool CompareAndSwap(long old, long @new) =>
            Interlocked.CompareExchange(ref _value, @new, old) == old;
    }

    [GoType("struct", Name = "Pointer", Package = "sync/atomic")]
    public class GoAtomicPointer
    {
        private object? _value;

        [GoMethod]
        public object? Load() => Volatile.Read(ref _value);

        [GoMethod]
        public void Store(object? val) => Volatile.Write(ref _value, val);

        [GoMethod]
        public object? Swap(object? val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public bool CompareAndSwap(object? old, object? @new) =>
            Interlocked.CompareExchange(ref _value, @new, old) == old;
    }

    [GoType("struct", Name = "Int32", Package = "sync/atomic")]
    public class GoAtomicInt32
    {
        private int _value;

        [GoMethod]
        public long Load() => Interlocked.CompareExchange(ref _value, 0, 0);

        [GoMethod]
        public void Store(long val) => Interlocked.Exchange(ref _value, (int)val);

        [GoMethod]
        public long Add(long delta) => Interlocked.Add(ref _value, (int)delta);

        [GoMethod]
        public long Swap(long val) => Interlocked.Exchange(ref _value, (int)val);

        [GoMethod]
        public bool CompareAndSwap(long old, long @new) =>
            Interlocked.CompareExchange(ref _value, (int)@new, (int)old) == (int)old;
    }

    [GoType("struct", Name = "Int64", Package = "sync/atomic")]
    public class GoAtomicInt64
    {
        private long _value;

        [GoMethod]
        public long Load() => Interlocked.Read(ref _value);

        [GoMethod]
        public void Store(long val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public long Add(long delta) => Interlocked.Add(ref _value, delta);

        [GoMethod]
        public long Swap(long val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public bool CompareAndSwap(long old, long @new) =>
            Interlocked.CompareExchange(ref _value, @new, old) == old;
    }

    [GoType("struct", Name = "Uint32", Package = "sync/atomic")]
    public class GoAtomicUint32
    {
        private int _value;

        [GoMethod]
        public long Load() => (uint)Interlocked.CompareExchange(ref _value, 0, 0);

        [GoMethod]
        public void Store(long val) => Interlocked.Exchange(ref _value, (int)val);

        [GoMethod]
        public long Add(long delta) => (uint)Interlocked.Add(ref _value, (int)delta);

        [GoMethod]
        public long Swap(long val) => (uint)Interlocked.Exchange(ref _value, (int)val);

        [GoMethod]
        public bool CompareAndSwap(long old, long @new) =>
            Interlocked.CompareExchange(ref _value, (int)@new, (int)old) == (int)old;
    }

    [GoType("struct", Name = "Uint64", Package = "sync/atomic")]
    public class GoAtomicUint64
    {
        private long _value;

        [GoMethod]
        public long Load() => Interlocked.Read(ref _value);

        [GoMethod]
        public void Store(long val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public long Add(long delta) => Interlocked.Add(ref _value, delta);

        [GoMethod]
        public long Swap(long val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public bool CompareAndSwap(long old, long @new) =>
            Interlocked.CompareExchange(ref _value, @new, old) == old;
    }

    [GoType("struct", Name = "Bool", Package = "sync/atomic")]
    public class GoAtomicBool
    {
        private int _value;

        [GoMethod]
        public bool Load() => Interlocked.CompareExchange(ref _value, 0, 0) != 0;

        [GoMethod]
        public void Store(bool val) => Interlocked.Exchange(ref _value, val ? 1 : 0);

        [GoMethod]
        public bool Swap(bool val) => Interlocked.Exchange(ref _value, val ? 1 : 0) != 0;

        [GoMethod]
        public bool CompareAndSwap(bool old, bool @new) =>
            Interlocked.CompareExchange(ref _value, @new ? 1 : 0, old ? 1 : 0) == (old ? 1 : 0);
    }

    [GoType("struct", Name = "Value", Package = "sync/atomic")]
    public class GoAtomicValue
    {
        private object? _value;

        [GoMethod]
        public object? Load() => Volatile.Read(ref _value);

        [GoMethod]
        public void Store(object? val) => Volatile.Write(ref _value, val);

        [GoMethod]
        public object? Swap(object? val) => Interlocked.Exchange(ref _value, val);

        [GoMethod]
        public bool CompareAndSwap(object? old, object? @new) =>
            Interlocked.CompareExchange(ref _value, @new, old) == old;
    }
}
