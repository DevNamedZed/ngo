using System.Reflection.Emit;
using System.Threading;
using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Provides .NET IL implementations for Go functions whose bodies are in
    /// assembly (.s) files and cannot run on .NET.
    ///
    /// When a Go source file declares a function with no body (the body is in asm),
    /// and ngo compiles that package from Go source, the function would be a no-op.
    /// This class intercepts those functions and emits the correct .NET equivalent.
    ///
    /// Examples:
    ///   sync/atomic.LoadInt32  → Volatile.Read
    ///   sync/atomic.StoreInt32 → Volatile.Write
    ///   runtime.NumCPU         → Environment.ProcessorCount
    /// </summary>
    internal static class RuntimeIntrinsics
    {
        /// <summary>
        /// Try to emit a .NET IL body for a known assembly-backed Go function.
        /// Returns true if handled (IL emitted + ret), false to fall through to default.
        /// </summary>
        public static bool TryEmitBody(EmitContext ctx, string name, string? packageName)
        {
            var pkg = packageName ?? "";

            return pkg switch
            {
                "atomic" or "sync/atomic" => TryEmitAtomic(ctx, name),
                "sync" => TryEmitSync(ctx, name),
                "runtime" => TryEmitRuntime(ctx, name),
                "syscall" => TryEmitSyscall(ctx, name),
                "math" => TryEmitMath(ctx, name),
                "internal/bytealg" => TryEmitBytealg(ctx, name),
                "unix" or "golang.org/x/sys/unix" => TryEmitUnix(ctx, name),
                _ => false,
            };
        }

        /// <summary>
        /// Try to emit by go:linkname target (e.g., "runtime.semacquire", "runtime.nanotime").
        /// The linkname format is "package.function" — split and dispatch.
        /// </summary>
        public static bool TryEmitByLinkName(EmitContext ctx, string linkName)
        {
            var dot = linkName.LastIndexOf('.');
            if (dot < 0) return false;

            var pkg = linkName.Substring(0, dot);
            var name = linkName.Substring(dot + 1);

            return pkg switch
            {
                "runtime" => TryEmitRuntime(ctx, name) || TryEmitSync(ctx, "runtime_" + name),
                "sync" => TryEmitSync(ctx, name),
                "sync/atomic" => TryEmitAtomic(ctx, name),
                _ => false,
            };
        }

        // ---- sync/atomic intrinsics ----
        // Go's sync/atomic functions are implemented in asm_amd64.s as single
        // CPU instructions (MOVL, LOCK XADDL, LOCK CMPXCHGL, etc.)
        // .NET equivalents: System.Threading.Volatile and System.Threading.Interlocked

        private static bool TryEmitAtomic(EmitContext ctx, string name)
        {
            var il = ctx.IL;

            switch (name)
            {
                // --- Loads ---
                // func LoadInt32(addr *int32) int32
                case "LoadInt32":
                    il.Emit(OpCodes.Ldarg_0); // addr
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Read", new[] { typeof(int).MakeByRefType() })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "LoadInt64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Read", new[] { typeof(long).MakeByRefType() })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "LoadUint32":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Read", new[] { typeof(uint).MakeByRefType() })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "LoadUint64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Read", new[] { typeof(ulong).MakeByRefType() })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "LoadUintptr":
                case "LoadPointer":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldind_I); // load native int from pointer
                    il.Emit(OpCodes.Ret);
                    return true;

                // --- Stores ---
                // func StoreInt32(addr *int32, val int32)
                case "StoreInt32":
                    il.Emit(OpCodes.Ldarg_0); // addr
                    il.Emit(OpCodes.Ldarg_1); // val
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Write", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "StoreInt64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Write", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "StoreUint32":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Write", new[] { typeof(uint).MakeByRefType(), typeof(uint) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "StoreUint64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Write", new[] { typeof(ulong).MakeByRefType(), typeof(ulong) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "StoreUintptr":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Stind_I); // store native int to pointer
                    il.Emit(OpCodes.Ret);
                    return true;

                // --- Add (returns old value) ---
                // func AddInt32(addr *int32, delta int32) (new int32)
                case "AddInt32":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "AddInt64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "AddUint32":
                    // Interlocked.Add works on int, cast through
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "AddUint64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "AddUintptr":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                // --- CompareAndSwap ---
                // func CompareAndSwapInt32(addr *int32, old, new int32) (swapped bool)
                case "CompareAndSwapInt32":
                    il.Emit(OpCodes.Ldarg_0); // addr
                    il.Emit(OpCodes.Ldarg_2); // new
                    il.Emit(OpCodes.Ldarg_1); // old (comparand)
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("CompareExchange", new[] { typeof(int).MakeByRefType(), typeof(int), typeof(int) })!);
                    il.Emit(OpCodes.Ldarg_1); // old
                    il.Emit(OpCodes.Ceq);     // result == old means swap succeeded
                    il.Emit(OpCodes.Ret);
                    return true;

                case "CompareAndSwapInt64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("CompareExchange", new[] { typeof(long).MakeByRefType(), typeof(long), typeof(long) })!);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "CompareAndSwapUint32":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("CompareExchange", new[] { typeof(int).MakeByRefType(), typeof(int), typeof(int) })!);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "CompareAndSwapUint64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("CompareExchange", new[] { typeof(long).MakeByRefType(), typeof(long), typeof(long) })!);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "CompareAndSwapUintptr":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("CompareExchange", new[] { typeof(long).MakeByRefType(), typeof(long), typeof(long) })!);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Ret);
                    return true;

                // --- Swap ---
                // func SwapInt32(addr *int32, new int32) (old int32)
                case "SwapInt32":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Exchange", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "SwapInt64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Exchange", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "SwapUint32":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Exchange", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "SwapUint64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Exchange", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "SwapUintptr":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Exchange", new[] { typeof(long).MakeByRefType(), typeof(long) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                default:
                    return false;
            }
        }

        // ---- sync intrinsics ----
        // Go's sync package uses go:linkname to call runtime semaphore functions.
        // These are the scheduler-aware blocking primitives.
        // .NET equivalents: Monitor, SemaphoreSlim, SpinWait.

        private static bool TryEmitSync(EmitContext ctx, string name)
        {
            var il = ctx.IL;

            switch (name)
            {
                // runtime_Semacquire(s *uint32) — block until *s > 0, then decrement
                case "runtime_Semacquire":
                    // Use SpinWait + Interlocked loop:
                    // while (Interlocked.CompareExchange(ref *s, *s-1, *s) != *s) { SpinWait; }
                    // Simplified: Monitor.Enter on a synthetic object isn't right.
                    // For now, emit a spin-wait CAS loop.
                    EmitSemacquire(il);
                    return true;

                // runtime_SemacquireMutex(s *uint32, lifo bool, skipframes int)
                case "runtime_SemacquireMutex":
                    // Same as Semacquire for our purposes (lifo/skipframes are scheduler hints)
                    EmitSemacquire(il);
                    return true;

                // runtime_Semrelease(s *uint32, handoff bool, skipframes int)
                case "runtime_Semrelease":
                    // Increment *s atomically
                    il.Emit(OpCodes.Ldarg_0);        // s *uint32
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Pop);             // discard return value
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_canSpin(i int) bool
                case "runtime_canSpin":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4_4);
                    il.Emit(OpCodes.Clt);             // i < 4
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_doSpin()
                case "runtime_doSpin":
                    il.Emit(OpCodes.Ldc_I4, 30);
                    il.Emit(OpCodes.Call, typeof(Thread).GetMethod("SpinWait", new[] { typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_nanotime() int64
                case "runtime_nanotime":
                case "runtime_nanotime1":
                    // DateTime.UtcNow.Ticks * 100 gives nanoseconds
                    il.Emit(OpCodes.Call, typeof(System.DateTime).GetProperty("UtcNow")!.GetGetMethod()!);
                    var dtLocal = il.DeclareLocal(typeof(System.DateTime));
                    il.Emit(OpCodes.Stloc, dtLocal);
                    il.Emit(OpCodes.Ldloca, dtLocal);
                    il.Emit(OpCodes.Call, typeof(System.DateTime).GetProperty("Ticks")!.GetGetMethod()!);
                    il.Emit(OpCodes.Ldc_I8, 100L);
                    il.Emit(OpCodes.Mul);
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_procPin() int
                case "runtime_procPin":
                    il.Emit(OpCodes.Ldc_I4_0);       // return 0 (P id, not meaningful on .NET)
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_procUnpin()
                case "runtime_procUnpin":
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_notifyListAdd(l *notifyList) uint32
                case "runtime_notifyListAdd":
                    // Return a ticket number — just increment atomically
                    il.Emit(OpCodes.Ldarg_0);  // notifyList pointer (treat as *uint32)
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Sub);       // return old value (Add returns new)
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_notifyListWait(l *notifyList, t uint32)
                case "runtime_notifyListWait":
                    // Block until notified — use Thread.SpinWait as approximation
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Call, typeof(Thread).GetMethod("SpinWait", new[] { typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_notifyListNotifyAll(l *notifyList)
                case "runtime_notifyListNotifyAll":
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_notifyListNotifyOne(l *notifyList)
                case "runtime_notifyListNotifyOne":
                    il.Emit(OpCodes.Ret);
                    return true;

                // runtime_notifyListCheck(size uintptr) — panics if wrong size
                case "runtime_notifyListCheck":
                    il.Emit(OpCodes.Ret);
                    return true;

                // throw(s string) — used by sync internals
                case "throw":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Newobj, typeof(System.Exception).GetConstructor(new[] { typeof(string) })!);
                    il.Emit(OpCodes.Throw);
                    return true;

                // fatal(s string) — same as throw
                case "fatal":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Newobj, typeof(System.Exception).GetConstructor(new[] { typeof(string) })!);
                    il.Emit(OpCodes.Throw);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Emit semaphore acquire: spin-wait loop doing CAS on *addr until we decrement it.
        /// </summary>
        private static void EmitSemacquire(CilWriter il)
        {
            // Loop:
            //   int old = Volatile.Read(ref *s);
            //   if (old > 0 && Interlocked.CompareExchange(ref *s, old-1, old) == old)
            //     return;
            //   Thread.SpinWait(1);
            //   goto Loop;
            var loopLabel = il.DefineLabel();
            var doneLabel = il.DefineLabel();
            var oldLocal = il.DeclareLocal(typeof(int));

            il.MarkLabel(loopLabel);
            // old = Volatile.Read(ref *s)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Volatile).GetMethod("Read", new[] { typeof(int).MakeByRefType() })!);
            il.Emit(OpCodes.Stloc, oldLocal);

            // if (old <= 0) goto spin
            il.Emit(OpCodes.Ldloc, oldLocal);
            il.Emit(OpCodes.Ldc_I4_0);
            var spinLabel = il.DefineLabel();
            il.Emit(OpCodes.Ble, spinLabel);

            // CAS: if (Interlocked.CompareExchange(ref *s, old-1, old) == old) return
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, oldLocal);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Sub);       // old - 1
            il.Emit(OpCodes.Ldloc, oldLocal);  // comparand = old
            il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("CompareExchange", new[] { typeof(int).MakeByRefType(), typeof(int), typeof(int) })!);
            il.Emit(OpCodes.Ldloc, oldLocal);
            il.Emit(OpCodes.Beq, doneLabel);

            // Spin and retry
            il.MarkLabel(spinLabel);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Call, typeof(Thread).GetMethod("SpinWait", new[] { typeof(int) })!);
            il.Emit(OpCodes.Br, loopLabel);

            il.MarkLabel(doneLabel);
            il.Emit(OpCodes.Ret);
        }

        // ---- runtime intrinsics ----
        // Go's runtime package — scheduler, GC, OS info.
        // Most of these map trivially to .NET equivalents.

        private static bool TryEmitRuntime(EmitContext ctx, string name)
        {
            var il = ctx.IL;

            switch (name)
            {
                case "NumCPU":
                    il.Emit(OpCodes.Call, typeof(System.Environment).GetProperty("ProcessorCount")!.GetGetMethod()!);
                    il.Emit(OpCodes.Conv_I8);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "GOMAXPROCS":
                    // GOMAXPROCS(n) returns previous value. On .NET, just return ProcessorCount.
                    il.Emit(OpCodes.Pop);  // discard n
                    il.Emit(OpCodes.Call, typeof(System.Environment).GetProperty("ProcessorCount")!.GetGetMethod()!);
                    il.Emit(OpCodes.Conv_I8);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Version":
                    il.Emit(OpCodes.Ldstr, "go1.22.6");
                    il.Emit(OpCodes.Ret);
                    return true;

                case "GOROOT":
                    il.Emit(OpCodes.Ldstr, "");
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Gosched":
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, typeof(Thread).GetMethod("Sleep", new[] { typeof(int) })!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "GC":
                    il.Emit(OpCodes.Call, typeof(System.GC).GetMethod("Collect", System.Type.EmptyTypes)!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "KeepAlive":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(System.GC).GetMethod("KeepAlive")!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "SetFinalizer":
                    // SetFinalizer(obj, finalizer) — not easily mapped to .NET finalizers
                    // No-op is safe (object will still be GC'd)
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Caller":
                    // Caller(skip) → (pc, file, line, ok)
                    // Return dummy values — full stack walking requires System.Diagnostics.StackTrace
                    il.Emit(OpCodes.Ldc_I8, 0L);     // pc = 0
                    il.Emit(OpCodes.Ldstr, "");       // file = ""
                    il.Emit(OpCodes.Ldc_I8, 0L);     // line = 0
                    il.Emit(OpCodes.Ldc_I4_0);        // ok = false
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Callers":
                    // Callers(skip, pc []uintptr) int — return 0
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Getenv":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(System.Environment).GetMethod("GetEnvironmentVariable", new[] { typeof(string) })!);
                    il.Emit(OpCodes.Dup);
                    var notNull = il.DefineLabel();
                    il.Emit(OpCodes.Brtrue, notNull);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldstr, "");
                    il.MarkLabel(notNull);
                    il.Emit(OpCodes.Ret);
                    return true;

                // nanotime() int64 — used by time, net, etc. via go:linkname
                case "nanotime":
                case "nanotime1":
                    il.Emit(OpCodes.Call, typeof(System.DateTime).GetProperty("UtcNow")!.GetGetMethod()!);
                    var dtLocal2 = il.DeclareLocal(typeof(System.DateTime));
                    il.Emit(OpCodes.Stloc, dtLocal2);
                    il.Emit(OpCodes.Ldloca, dtLocal2);
                    il.Emit(OpCodes.Call, typeof(System.DateTime).GetProperty("Ticks")!.GetGetMethod()!);
                    il.Emit(OpCodes.Ldc_I8, 100L);
                    il.Emit(OpCodes.Mul);
                    il.Emit(OpCodes.Ret);
                    return true;

                // walltime() (sec int64, nsec int32)
                case "walltime":
                case "walltime1":
                    il.Emit(OpCodes.Call, typeof(System.DateTimeOffset).GetProperty("UtcNow")!.GetGetMethod()!);
                    var dtoLocal = il.DeclareLocal(typeof(System.DateTimeOffset));
                    il.Emit(OpCodes.Stloc, dtoLocal);
                    il.Emit(OpCodes.Ldloca, dtoLocal);
                    il.Emit(OpCodes.Call, typeof(System.DateTimeOffset).GetMethod("ToUnixTimeSeconds")!);
                    il.Emit(OpCodes.Ldloca, dtoLocal);
                    il.Emit(OpCodes.Call, typeof(System.DateTimeOffset).GetProperty("Millisecond")!.GetGetMethod()!);
                    il.Emit(OpCodes.Ldc_I4, 1000000);
                    il.Emit(OpCodes.Mul);           // ms → ns
                    il.Emit(OpCodes.Conv_I4);
                    il.Emit(OpCodes.Ret);
                    return true;

                // semacquire/semrelease — used by many packages via go:linkname
                case "semacquire":
                case "semacquire1":
                    EmitSemacquire(il);
                    return true;

                case "semrelease":
                case "semrelease1":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Call, typeof(Interlocked).GetMethod("Add", new[] { typeof(int).MakeByRefType(), typeof(int) })!);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ret);
                    return true;

                // mcall, systemstack — scheduler internals, run the function directly
                case "mcall":
                case "systemstack":
                    // These take a function and run it on the system stack.
                    // On .NET, just call it directly.
                    il.Emit(OpCodes.Ret); // no-op — the caller handles the function
                    return true;

                // memmove(to, from unsafe.Pointer, n uintptr)
                case "memmove":
                    il.Emit(OpCodes.Ldarg_0);  // to
                    il.Emit(OpCodes.Ldarg_1);  // from
                    il.Emit(OpCodes.Ldarg_2);  // n
                    il.Emit(OpCodes.Cpblk);
                    il.Emit(OpCodes.Ret);
                    return true;

                // memclrNoHeapPointers(ptr unsafe.Pointer, n uintptr)
                case "memclrNoHeapPointers":
                    il.Emit(OpCodes.Ldarg_0);  // ptr
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldarg_1);  // n
                    il.Emit(OpCodes.Initblk);
                    il.Emit(OpCodes.Ret);
                    return true;

                // ---- reflect / runtime type system functions ----
                // These are called via go:linkname from reflect, fmt, encoding/json, etc.

                // typedmemmove(typ *_type, dst, src unsafe.Pointer)
                case "typedmemmove":
                case "typedmemclr":
                case "typedslicecopy":
                    // Memory move/clear — on .NET the GC handles this.
                    // These are used by reflect for Set operations.
                    // No-op is safe for correctness (the actual copy happens at the Go level).
                    il.Emit(OpCodes.Ret);
                    return true;

                // ifaceIndir(t *_type) bool — does interface value need indirection?
                case "ifaceIndir":
                    il.Emit(OpCodes.Ldc_I4_0); // false — .NET handles boxing
                    il.Emit(OpCodes.Ret);
                    return true;

                // mapaccess1(t *maptype, h *hmap, key unsafe.Pointer) unsafe.Pointer
                case "mapaccess1":
                case "mapaccess1_fast32":
                case "mapaccess1_fast64":
                case "mapaccess1_faststr":
                case "mapaccess2":
                case "mapaccess2_fast32":
                case "mapaccess2_fast64":
                case "mapaccess2_faststr":
                    // Map access — return nil pointer (reflect handles map access via Go source)
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // mapassign(t *maptype, h *hmap, key unsafe.Pointer) unsafe.Pointer
                case "mapassign":
                case "mapassign_fast32":
                case "mapassign_fast64":
                case "mapassign_faststr":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // mapdelete(t *maptype, h *hmap, key unsafe.Pointer)
                case "mapdelete":
                case "mapdelete_fast32":
                case "mapdelete_fast64":
                case "mapdelete_faststr":
                    il.Emit(OpCodes.Ret);
                    return true;

                // mapiterinit(t *maptype, h *hmap, it *hiter)
                case "mapiterinit":
                case "mapiternext":
                    il.Emit(OpCodes.Ret);
                    return true;

                // makemap(t *maptype, hint int, h *hmap) *hmap
                case "makemap":
                case "makemap64":
                case "makemap_small":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // makeslice(et *_type, len, cap int) unsafe.Pointer
                case "makeslice":
                case "makeslice64":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // growslice(et *_type, old slice, cap int) slice
                case "growslice":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // makechan(t *chantype, size int) *hchan
                case "makechan":
                case "makechan64":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // chanrecv(c *hchan, ep unsafe.Pointer, block bool) (selected, received bool)
                case "chanrecv1":
                case "chanrecv2":
                case "chansend1":
                    il.Emit(OpCodes.Ret);
                    return true;

                // closechan(c *hchan)
                case "closechan":
                    il.Emit(OpCodes.Ret);
                    return true;

                // convT(t *_type, v unsafe.Pointer) unsafe.Pointer
                case "convT":
                case "convT16":
                case "convT32":
                case "convT64":
                case "convTstring":
                case "convTslice":
                    // Type conversion for interface boxing — return the pointer as-is
                    il.Emit(OpCodes.Ldarg_1); // v
                    il.Emit(OpCodes.Ret);
                    return true;

                // newobject(t *_type) unsafe.Pointer
                case "newobject":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // mallocgc(size uintptr, typ *_type, needzero bool) unsafe.Pointer
                case "mallocgc":
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;

                // rand() uint64 — runtime random number
                case "rand":
                    il.Emit(OpCodes.Call, typeof(System.Random).GetProperty("Shared")!.GetGetMethod()!);
                    il.Emit(OpCodes.Call, typeof(System.Random).GetMethod("NextInt64")!);
                    il.Emit(OpCodes.Ret);
                    return true;

                // Goroutine/scheduler functions — no-ops on .NET
                case "Goexit":
                    il.Emit(OpCodes.Newobj, typeof(System.Threading.ThreadInterruptedException).GetConstructor(System.Type.EmptyTypes)!);
                    il.Emit(OpCodes.Throw);
                    return true;

                case "LockOSThread":
                case "UnlockOSThread":
                    il.Emit(OpCodes.Ret);
                    return true;

                // Memory stats
                case "ReadMemStats":
                    // No-op — Go's MemStats struct is specific to gc runtime
                    il.Emit(OpCodes.Ret);
                    return true;

                default:
                    return false;
            }
        }

        // ---- syscall intrinsics ----
        // Go's syscall.Syscall/RawSyscall are assembly stubs that execute SYSCALL.
        // On .NET we can't do raw syscalls. These need to map to .NET BCL operations.
        // For now, return ENOSYS. The higher-level Go wrappers (Open, Read, Write, etc.)
        // are compiled from Go source and call these — they'll get proper errors.

        private static bool TryEmitSyscall(EmitContext ctx, string name)
        {
            var il = ctx.IL;

            switch (name)
            {
                // syscall.Syscall(trap, a1, a2, a3) → (r1, r2, err)
                // Dispatch to Ngo.Runtime.SyscallBridge.Syscall3 which P/Invokes to libc
                case "Syscall":
                case "RawSyscall":
                    il.Emit(OpCodes.Ldarg_0); // trap
                    il.Emit(OpCodes.Ldarg_1); // a1
                    il.Emit(OpCodes.Ldarg_2); // a2
                    il.Emit(OpCodes.Ldarg_3); // a3
                    il.Emit(OpCodes.Call, typeof(Ngo.Runtime.SyscallBridge).GetMethod("Syscall3")!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Syscall6":
                case "RawSyscall6":
                    il.Emit(OpCodes.Ldarg_0); // trap
                    il.Emit(OpCodes.Ldarg_1); // a1
                    il.Emit(OpCodes.Ldarg_2); // a2
                    il.Emit(OpCodes.Ldarg_3); // a3
                    il.Emit(OpCodes.Ldarg_S, (byte)4); // a4
                    il.Emit(OpCodes.Ldarg_S, (byte)5); // a5
                    il.Emit(OpCodes.Ldarg_S, (byte)6); // a6
                    il.Emit(OpCodes.Call, typeof(Ngo.Runtime.SyscallBridge).GetMethod("Syscall6")!);
                    il.Emit(OpCodes.Ret);
                    return true;

                default:
                    return false;
            }
        }

        // ---- math intrinsics ----
        // Go's math package has assembly-optimized versions of common functions.
        // Pure Go fallbacks exist for all of them, but some are asm-only on amd64.
        // Map to System.Math equivalents.

        private static bool TryEmitMath(EmitContext ctx, string name)
        {
            var il = ctx.IL;

            // Map Go math assembly functions to System.Math
            var mathMethod = name switch
            {
                "Sqrt" => typeof(System.Math).GetMethod("Sqrt"),
                "Floor" => typeof(System.Math).GetMethod("Floor", new[] { typeof(double) }),
                "Ceil" => typeof(System.Math).GetMethod("Ceiling", new[] { typeof(double) }),
                "Trunc" => typeof(System.Math).GetMethod("Truncate", new[] { typeof(double) }),
                "Abs" => typeof(System.Math).GetMethod("Abs", new[] { typeof(double) }),
                "Log" => typeof(System.Math).GetMethod("Log", new[] { typeof(double) }),
                "Log2" => typeof(System.Math).GetMethod("Log2"),
                "Log10" => typeof(System.Math).GetMethod("Log10"),
                "Exp" => typeof(System.Math).GetMethod("Exp"),
                "Sin" => typeof(System.Math).GetMethod("Sin"),
                "Cos" => typeof(System.Math).GetMethod("Cos"),
                "Tan" => typeof(System.Math).GetMethod("Tan"),
                "Asin" => typeof(System.Math).GetMethod("Asin"),
                "Acos" => typeof(System.Math).GetMethod("Acos"),
                "Atan" => typeof(System.Math).GetMethod("Atan"),
                "Atan2" => typeof(System.Math).GetMethod("Atan2"),
                "Pow" => typeof(System.Math).GetMethod("Pow"),
                "Cbrt" => typeof(System.Math).GetMethod("Cbrt"),
                "Round" => typeof(System.Math).GetMethod("Round", new[] { typeof(double) }),
                "RoundToEven" => typeof(System.Math).GetMethod("Round", new[] { typeof(double), typeof(System.MidpointRounding) }),
                "Mod" or "Remainder" => typeof(System.Math).GetMethod("IEEERemainder"),
                "Copysign" => typeof(System.Math).GetMethod("CopySign"),
                "FMA" => typeof(System.Math).GetMethod("FusedMultiplyAdd"),
                "Min" => typeof(System.Math).GetMethod("Min", new[] { typeof(double), typeof(double) }),
                "Max" => typeof(System.Math).GetMethod("Max", new[] { typeof(double), typeof(double) }),
                "Hypot" or "hypot" => null, // No direct equivalent, has Go fallback
                "Ldexp" or "ldexp" => null, // Has Go fallback
                "Frexp" or "frexp" => null, // Has Go fallback
                "Modf" or "modf" => null,   // Has Go fallback
                _ => null,
            };

            if (mathMethod == null) return false;

            // Emit: load args, call System.Math method, return
            var paramCount = mathMethod.GetParameters().Length;
            if (paramCount >= 1) il.Emit(OpCodes.Ldarg_0);
            if (paramCount >= 2) il.Emit(OpCodes.Ldarg_1);

            // Special case: RoundToEven needs MidpointRounding.ToEven
            if (name == "RoundToEven")
            {
                il.Emit(OpCodes.Ldc_I4, (int)System.MidpointRounding.ToEven);
            }

            il.Emit(OpCodes.Call, mathMethod);
            il.Emit(OpCodes.Ret);
            return true;
        }

        // ---- internal/bytealg intrinsics ----
        // Go's internal/bytealg has assembly-optimized string/byte search functions.

        private static bool TryEmitBytealg(EmitContext ctx, string name)
        {
            var il = ctx.IL;
            var bytealg = typeof(Ngo.Runtime.Internal.Bytealg.Package);

            switch (name)
            {
                // Native functions — delegate to C# implementations in Ngo.Runtime
                case "IndexByte":
                case "IndexByteString":
                case "Count":
                case "CountString":
                case "Compare":
                case "Index":
                case "IndexString":
                case "Equal":
                case "LastIndexByte":
                case "LastIndexByteString":
                case "IndexRabinKarp":
                case "IndexRabinKarpBytes":
                case "LastIndexRabinKarp":
                case "Cutover":
                case "MakeNoZero":
                case "HashStr":
                case "HashStrRev":
                case "HashStrRevBytes":
                case "HashStrBytes":
                {
                    // Find matching method in C# runtime by name
                    var methods = bytealg.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    foreach (var m in methods)
                    {
                        if (m.Name == name)
                        {
                            var ps = m.GetParameters();
                            for (int i = 0; i < ps.Length; i++)
                                il.Emit(OpCodes.Ldarg, i);
                            il.Emit(OpCodes.Call, m);
                            il.Emit(OpCodes.Ret);
                            return true;
                        }
                    }
                    // Fallback: return default
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ret);
                    return true;
                }

                // abigen_* are runtime internal — no-ops
                case "abigen_runtime_cmpstring":
                case "abigen_runtime_memequal":
                case "abigen_runtime_memequal_varlen":
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ret);
                    return true;

                default:
                    return false;
            }
        }

        // ---- golang.org/x/sys/unix intrinsics ----
        // Assembly-backed syscall wrappers from the x/sys package.

        private static bool TryEmitUnix(EmitContext ctx, string name)
        {
            var il = ctx.IL;

            switch (name)
            {
                // Syscall wrappers — delegate to SyscallBridge
                case "Syscall":
                case "RawSyscall":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_3);
                    il.Emit(OpCodes.Call, typeof(Ngo.Runtime.SyscallBridge).GetMethod("Syscall3")!);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "Syscall6":
                case "RawSyscall6":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_3);
                    il.Emit(OpCodes.Ldarg_S, (byte)4);
                    il.Emit(OpCodes.Ldarg_S, (byte)5);
                    il.Emit(OpCodes.Ldarg_S, (byte)6);
                    il.Emit(OpCodes.Call, typeof(Ngo.Runtime.SyscallBridge).GetMethod("Syscall6")!);
                    il.Emit(OpCodes.Ret);
                    return true;

                // Terminal functions
                case "IoctlGetTermios":
                case "IoctlSetTermios":
                case "IoctlGetWinsize":
                    // Return zero/nil — terminal ioctls not critical for most apps
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ret);
                    return true;

                case "IsTerminal":
                    // Check if fd is a terminal — return false (safe default)
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ret);
                    return true;

                // Common syscall wrappers
                case "Close":
                case "Read":
                case "Write":
                case "Open":
                case "Openat":
                case "Fstat":
                case "Stat":
                case "Lstat":
                    // These have Go source implementations that wrap Syscall()
                    // If they're body-less, just return defaults
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ret);
                    return true;

                default:
                    // Any unknown x/sys/unix function — return zero/nil
                    il.Emit(OpCodes.Ret);
                    return true;
            }
        }
    }
}
