using System.Threading;
using Ngo.Runtime.Discovery;
using CompilerUnsafe = System.Runtime.CompilerServices.Unsafe;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoPackage("sync/atomic")]
    public static class Package
    {
        [GoType("struct", Name = "Bool", Package = "sync/atomic")]
        public sealed class Bool
        {
            [GoField(Name = "v", Type = "uint32")]
            private int _value;

            [GoMethod]
            public bool Load()
            {
                return Interlocked.CompareExchange(ref _value, 0, 0) != 0;
            }

            [GoMethod]
            public void Store(bool value)
            {
                Interlocked.Exchange(ref _value, value ? 1 : 0);
            }

            [GoMethod]
            public bool Swap(bool value)
            {
                return Interlocked.Exchange(ref _value, value ? 1 : 0) != 0;
            }

            [GoMethod]
            public bool CompareAndSwap(bool oldValue, bool newValue)
            {
                int expectedOld = oldValue ? 1 : 0;
                int desired = newValue ? 1 : 0;
                return Interlocked.CompareExchange(ref _value, desired, expectedOld) == expectedOld;
            }
        }

        [GoType("struct", Name = "Int32", Package = "sync/atomic")]
        public sealed class Int32
        {
            [GoField(Name = "v", Type = "int32")]
            private int _value;

            [GoMethod]
            [return: GoReturn("int32")]
            public int Load()
            {
                return Interlocked.CompareExchange(ref _value, 0, 0);
            }

            [GoMethod]
            public void Store([GoParam("int32")] int value)
            {
                Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            [return: GoReturn("int32")]
            public int Swap([GoParam("int32")] int value)
            {
                return Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("int32")] int oldValue, [GoParam("int32")] int newValue)
            {
                return Interlocked.CompareExchange(ref _value, newValue, oldValue) == oldValue;
            }

            [GoMethod]
            [return: GoReturn("int32")]
            public int Add([GoParam("int32")] int delta)
            {
                return Interlocked.Add(ref _value, delta);
            }
        }

        [GoType("struct", Name = "Int64", Package = "sync/atomic")]
        public sealed class Int64
        {
            [GoField(Name = "v", Type = "int64")]
            private long _value;

            [GoMethod]
            [return: GoReturn("int64")]
            public long Load()
            {
                return Interlocked.Read(ref _value);
            }

            [GoMethod]
            public void Store([GoParam("int64")] long value)
            {
                Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            [return: GoReturn("int64")]
            public long Swap([GoParam("int64")] long value)
            {
                return Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("int64")] long oldValue, [GoParam("int64")] long newValue)
            {
                return Interlocked.CompareExchange(ref _value, newValue, oldValue) == oldValue;
            }

            [GoMethod]
            [return: GoReturn("int64")]
            public long Add([GoParam("int64")] long delta)
            {
                return Interlocked.Add(ref _value, delta);
            }
        }

        [GoType("struct", Name = "Uint32", Package = "sync/atomic")]
        public sealed class Uint32
        {
            [GoField(Name = "v", Type = "uint32")]
            private int _value;

            [GoMethod]
            [return: GoReturn("uint32")]
            public uint Load()
            {
                int result = Interlocked.CompareExchange(ref _value, 0, 0);
                return CompilerUnsafe.As<int, uint>(ref result);
            }

            [GoMethod]
            public void Store([GoParam("uint32")] uint value)
            {
                int signed = CompilerUnsafe.As<uint, int>(ref value);
                Interlocked.Exchange(ref _value, signed);
            }

            [GoMethod]
            [return: GoReturn("uint32")]
            public uint Swap([GoParam("uint32")] uint value)
            {
                int signed = CompilerUnsafe.As<uint, int>(ref value);
                int previous = Interlocked.Exchange(ref _value, signed);
                return CompilerUnsafe.As<int, uint>(ref previous);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("uint32")] uint oldValue, [GoParam("uint32")] uint newValue)
            {
                int signedOld = CompilerUnsafe.As<uint, int>(ref oldValue);
                int signedNew = CompilerUnsafe.As<uint, int>(ref newValue);
                return Interlocked.CompareExchange(ref _value, signedNew, signedOld) == signedOld;
            }

            [GoMethod]
            [return: GoReturn("uint32")]
            public uint Add([GoParam("uint32")] uint delta)
            {
                int signedDelta = CompilerUnsafe.As<uint, int>(ref delta);
                int result = Interlocked.Add(ref _value, signedDelta);
                return CompilerUnsafe.As<int, uint>(ref result);
            }
        }

        [GoType("struct", Name = "Uint64", Package = "sync/atomic")]
        public sealed class Uint64
        {
            [GoField(Name = "v", Type = "uint64")]
            private long _value;

            [GoMethod]
            [return: GoReturn("uint64")]
            public ulong Load()
            {
                long result = Interlocked.Read(ref _value);
                return CompilerUnsafe.As<long, ulong>(ref result);
            }

            [GoMethod]
            public void Store([GoParam("uint64")] ulong value)
            {
                long signed = CompilerUnsafe.As<ulong, long>(ref value);
                Interlocked.Exchange(ref _value, signed);
            }

            [GoMethod]
            [return: GoReturn("uint64")]
            public ulong Swap([GoParam("uint64")] ulong value)
            {
                long signed = CompilerUnsafe.As<ulong, long>(ref value);
                long previous = Interlocked.Exchange(ref _value, signed);
                return CompilerUnsafe.As<long, ulong>(ref previous);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("uint64")] ulong oldValue, [GoParam("uint64")] ulong newValue)
            {
                long signedOld = CompilerUnsafe.As<ulong, long>(ref oldValue);
                long signedNew = CompilerUnsafe.As<ulong, long>(ref newValue);
                return Interlocked.CompareExchange(ref _value, signedNew, signedOld) == signedOld;
            }

            [GoMethod]
            [return: GoReturn("uint64")]
            public ulong Add([GoParam("uint64")] ulong delta)
            {
                long signedDelta = CompilerUnsafe.As<ulong, long>(ref delta);
                long result = Interlocked.Add(ref _value, signedDelta);
                return CompilerUnsafe.As<long, ulong>(ref result);
            }
        }

        [GoType("struct", Name = "Uintptr", Package = "sync/atomic")]
        public sealed class Uintptr
        {
            [GoField(Name = "v", Type = "uintptr")]
            private long _value;

            [GoMethod]
            [return: GoReturn("uintptr")]
            public long Load()
            {
                return Interlocked.Read(ref _value);
            }

            [GoMethod]
            public void Store([GoParam("uintptr")] long value)
            {
                Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            [return: GoReturn("uintptr")]
            public long Swap([GoParam("uintptr")] long value)
            {
                return Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("uintptr")] long oldValue, [GoParam("uintptr")] long newValue)
            {
                return Interlocked.CompareExchange(ref _value, newValue, oldValue) == oldValue;
            }

            [GoMethod]
            [return: GoReturn("uintptr")]
            public long Add([GoParam("uintptr")] long delta)
            {
                return Interlocked.Add(ref _value, delta);
            }
        }

        [GoType("struct", Name = "Value", Package = "sync/atomic")]
        public sealed class Value
        {
            [GoField(Name = "v", Type = "interface{}")]
            private object? _value;

            [GoMethod]
            [return: GoReturn("interface{}")]
            public object? Load()
            {
                return Volatile.Read(ref _value);
            }

            [GoMethod]
            public void Store([GoParam("interface{}")] object? value)
            {
                Volatile.Write(ref _value, value);
            }

            [GoMethod]
            [return: GoReturn("interface{}")]
            public object? Swap([GoParam("interface{}")] object? value)
            {
                return Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("interface{}")] object? oldValue, [GoParam("interface{}")] object? newValue)
            {
                return Interlocked.CompareExchange(ref _value, newValue, oldValue) == oldValue;
            }
        }

        [GoType("struct", Name = "Pointer", Package = "sync/atomic", TypeParams = "T")]
        public sealed class Pointer<T>
        {
            [GoField(Name = "v", Type = "*T")]
            private object? _value;

            [GoMethod]
            [return: GoReturn("*T")]
            public Ptr<T>? Load()
            {
                return (Ptr<T>?)Volatile.Read(ref _value);
            }

            [GoMethod]
            public void Store([GoParam("*T")] Ptr<T>? value)
            {
                Volatile.Write(ref _value, value);
            }

            [GoMethod]
            [return: GoReturn("*T")]
            public Ptr<T>? Swap([GoParam("*T")] Ptr<T>? value)
            {
                return (Ptr<T>?)Interlocked.Exchange(ref _value, value);
            }

            [GoMethod]
            public bool CompareAndSwap([GoParam("*T")] Ptr<T>? oldValue, [GoParam("*T")] Ptr<T>? newValue)
            {
                return Interlocked.CompareExchange(ref _value, newValue, oldValue) == oldValue;
            }
        }

        [GoFunc]
        [return: GoReturn("int32")]
        public static int LoadInt32(Ptr<int> address)
        {
            return Interlocked.CompareExchange(ref address.Value, 0, 0);
        }

        [GoFunc]
        [return: GoReturn("int64")]
        public static long LoadInt64(Ptr<long> address)
        {
            return Interlocked.Read(ref address.Value);
        }

        [GoFunc]
        [return: GoReturn("uint32")]
        public static uint LoadUint32(Ptr<uint> address)
        {
            ref int signed = ref CompilerUnsafe.As<uint, int>(ref address.Value);
            int result = Interlocked.CompareExchange(ref signed, 0, 0);
            return CompilerUnsafe.As<int, uint>(ref result);
        }

        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong LoadUint64(Ptr<ulong> address)
        {
            ref long signed = ref CompilerUnsafe.As<ulong, long>(ref address.Value);
            long result = Interlocked.Read(ref signed);
            return CompilerUnsafe.As<long, ulong>(ref result);
        }

        [GoFunc]
        [return: GoReturn("unsafe.Pointer")]
        public static long LoadPointer(Ptr<long> address)
        {
            return Interlocked.Read(ref address.Value);
        }

        [GoFunc]
        public static void StoreInt32(Ptr<int> address, [GoParam("int32")] int value)
        {
            Interlocked.Exchange(ref address.Value, value);
        }

        [GoFunc]
        public static void StoreInt64(Ptr<long> address, [GoParam("int64")] long value)
        {
            Interlocked.Exchange(ref address.Value, value);
        }

        [GoFunc]
        public static void StoreUint32(Ptr<uint> address, [GoParam("uint32")] uint value)
        {
            ref int signed = ref CompilerUnsafe.As<uint, int>(ref address.Value);
            int signedValue = CompilerUnsafe.As<uint, int>(ref value);
            Interlocked.Exchange(ref signed, signedValue);
        }

        [GoFunc]
        public static void StoreUint64(Ptr<ulong> address, [GoParam("uint64")] ulong value)
        {
            ref long signed = ref CompilerUnsafe.As<ulong, long>(ref address.Value);
            long signedValue = CompilerUnsafe.As<ulong, long>(ref value);
            Interlocked.Exchange(ref signed, signedValue);
        }

        [GoFunc]
        public static void StorePointer(Ptr<long> address, [GoParam("unsafe.Pointer")] long value)
        {
            Interlocked.Exchange(ref address.Value, value);
        }

        [GoFunc]
        [return: GoReturn("int32")]
        public static int AddInt32(Ptr<int> address, [GoParam("int32")] int delta)
        {
            return Interlocked.Add(ref address.Value, delta);
        }

        [GoFunc]
        [return: GoReturn("int64")]
        public static long AddInt64(Ptr<long> address, [GoParam("int64")] long delta)
        {
            return Interlocked.Add(ref address.Value, delta);
        }

        [GoFunc]
        [return: GoReturn("uint32")]
        public static uint AddUint32(Ptr<uint> address, [GoParam("uint32")] uint delta)
        {
            ref int signed = ref CompilerUnsafe.As<uint, int>(ref address.Value);
            int signedDelta = CompilerUnsafe.As<uint, int>(ref delta);
            int result = Interlocked.Add(ref signed, signedDelta);
            return CompilerUnsafe.As<int, uint>(ref result);
        }

        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong AddUint64(Ptr<ulong> address, [GoParam("uint64")] ulong delta)
        {
            ref long signed = ref CompilerUnsafe.As<ulong, long>(ref address.Value);
            long signedDelta = CompilerUnsafe.As<ulong, long>(ref delta);
            long result = Interlocked.Add(ref signed, signedDelta);
            return CompilerUnsafe.As<long, ulong>(ref result);
        }

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long AddUintptr(Ptr<long> address, [GoParam("uintptr")] long delta)
        {
            return Interlocked.Add(ref address.Value, delta);
        }

        [GoFunc]
        public static bool CompareAndSwapInt32(Ptr<int> address, [GoParam("int32")] int oldValue, [GoParam("int32")] int newValue)
        {
            return Interlocked.CompareExchange(ref address.Value, newValue, oldValue) == oldValue;
        }

        [GoFunc]
        public static bool CompareAndSwapInt64(Ptr<long> address, [GoParam("int64")] long oldValue, [GoParam("int64")] long newValue)
        {
            return Interlocked.CompareExchange(ref address.Value, newValue, oldValue) == oldValue;
        }

        [GoFunc]
        public static bool CompareAndSwapUint32(Ptr<uint> address, [GoParam("uint32")] uint oldValue, [GoParam("uint32")] uint newValue)
        {
            ref int signed = ref CompilerUnsafe.As<uint, int>(ref address.Value);
            int signedOld = CompilerUnsafe.As<uint, int>(ref oldValue);
            int signedNew = CompilerUnsafe.As<uint, int>(ref newValue);
            return Interlocked.CompareExchange(ref signed, signedNew, signedOld) == signedOld;
        }

        [GoFunc]
        public static bool CompareAndSwapUint64(Ptr<ulong> address, [GoParam("uint64")] ulong oldValue, [GoParam("uint64")] ulong newValue)
        {
            ref long signed = ref CompilerUnsafe.As<ulong, long>(ref address.Value);
            long signedOld = CompilerUnsafe.As<ulong, long>(ref oldValue);
            long signedNew = CompilerUnsafe.As<ulong, long>(ref newValue);
            return Interlocked.CompareExchange(ref signed, signedNew, signedOld) == signedOld;
        }

        [GoFunc]
        public static bool CompareAndSwapPointer(Ptr<long> address, [GoParam("unsafe.Pointer")] long oldValue, [GoParam("unsafe.Pointer")] long newValue)
        {
            return Interlocked.CompareExchange(ref address.Value, newValue, oldValue) == oldValue;
        }

        [GoFunc]
        [return: GoReturn("int32")]
        public static int SwapInt32(Ptr<int> address, [GoParam("int32")] int newValue)
        {
            return Interlocked.Exchange(ref address.Value, newValue);
        }

        [GoFunc]
        [return: GoReturn("int64")]
        public static long SwapInt64(Ptr<long> address, [GoParam("int64")] long newValue)
        {
            return Interlocked.Exchange(ref address.Value, newValue);
        }

        [GoFunc]
        [return: GoReturn("uint32")]
        public static uint SwapUint32(Ptr<uint> address, [GoParam("uint32")] uint newValue)
        {
            ref int signed = ref CompilerUnsafe.As<uint, int>(ref address.Value);
            int signedNew = CompilerUnsafe.As<uint, int>(ref newValue);
            int previous = Interlocked.Exchange(ref signed, signedNew);
            return CompilerUnsafe.As<int, uint>(ref previous);
        }

        [GoFunc]
        [return: GoReturn("uint64")]
        public static ulong SwapUint64(Ptr<ulong> address, [GoParam("uint64")] ulong newValue)
        {
            ref long signed = ref CompilerUnsafe.As<ulong, long>(ref address.Value);
            long signedNew = CompilerUnsafe.As<ulong, long>(ref newValue);
            long previous = Interlocked.Exchange(ref signed, signedNew);
            return CompilerUnsafe.As<long, ulong>(ref previous);
        }

        [GoFunc]
        [return: GoReturn("unsafe.Pointer")]
        public static long SwapPointer(Ptr<long> address, [GoParam("unsafe.Pointer")] long newValue)
        {
            return Interlocked.Exchange(ref address.Value, newValue);
        }

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long SwapUintptr(Ptr<long> address, [GoParam("uintptr")] long newValue)
        {
            return Interlocked.Exchange(ref address.Value, newValue);
        }

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long LoadUintptr(Ptr<long> address)
        {
            return Interlocked.Read(ref address.Value);
        }

        [GoFunc]
        public static void StoreUintptr(Ptr<long> address, [GoParam("uintptr")] long value)
        {
            Interlocked.Exchange(ref address.Value, value);
        }

        [GoFunc]
        public static bool CompareAndSwapUintptr(Ptr<long> address, [GoParam("uintptr")] long oldValue, [GoParam("uintptr")] long newValue)
        {
            return Interlocked.CompareExchange(ref address.Value, newValue, oldValue) == oldValue;
        }
    }
}
