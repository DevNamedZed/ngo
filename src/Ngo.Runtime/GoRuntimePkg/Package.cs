// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.GoRuntimePkg
{
    /// <summary>
    /// Go runtime package — stub implementations for .NET.
    /// </summary>
    [GoPackage("runtime")]
    public static class GoRuntime
    {
        public static readonly string GOOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" : "linux";

        public static readonly string GOARCH = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "386",
            Architecture.Arm => "arm",
            _ => "unknown",
        };

        public static GoRuntimeFunc? FuncForPC(long pc)
        {
            // .NET doesn't have direct PC->function mapping like Go
            // Return a stub that provides basic info from the stack trace
            return new GoRuntimeFunc(pc);
        }

        public static long Callers(long skip, Slice<long> pc)
        {
            var trace = new StackTrace((int)skip + 1, true);
            var frames = trace.GetFrames();
            if (frames == null) return 0;

            var count = global::System.Math.Min(frames.Length, pc.Len);
            for (int i = 0; i < count; i++)
            {
                // Use a hash of method + offset as a pseudo-PC
                var frame = frames[i];
                pc[i] = frame.GetILOffset() + (frame.GetMethod()?.GetHashCode() ?? 0);
            }
            return count;
        }

        [return: GoReturn("uintptr", "string", "int", "bool")]
        public static (long pc, string file, long line, bool ok) Caller(long skip)
        {
            var trace = new StackTrace((int)skip + 1, true);
            var frame = trace.GetFrame(0);
            if (frame == null)
                return (0, "", 0, false);

            return (
                frame.GetILOffset(),
                frame.GetFileName() ?? "unknown",
                frame.GetFileLineNumber(),
                true
            );
        }

        public static void GC()
        {
            System.GC.Collect();
        }

        public static void Gosched()
        {
            Thread.Yield();
        }

        public static long NumCPU()
        {
            return Environment.ProcessorCount;
        }

        public static long NumGoroutine()
        {
            return ThreadPool.ThreadCount;
        }

        public static long GOMAXPROCS(long n)
        {
            // .NET manages its own thread pool; return processor count
            return Environment.ProcessorCount;
        }

        [GoFunc]
        public static void ReadMemStats([GoParam("*MemStats")] object? stats)
        {
            GoMemStats? m = stats as GoMemStats;
            if (m == null) return;
            var gcInfo = System.GC.GetGCMemoryInfo();
            m.Alloc = gcInfo.HeapSizeBytes;
            m.TotalAlloc = gcInfo.HeapSizeBytes;
            m.Sys = (long)System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            m.HeapAlloc = gcInfo.HeapSizeBytes;
            m.HeapSys = gcInfo.HeapSizeBytes;
            m.NumGC = System.GC.CollectionCount(0);
        }

        public static void SetFinalizer(object? obj, object? finalizer)
        {
            // No direct equivalent in .NET — GC handles finalization
        }

        public static string GOROOT()
        {
            return Environment.GetEnvironmentVariable("GOROOT") ?? "/usr/local/go";
        }

        public static string Version()
        {
            return "go1.22.6";
        }

        public static long Stack(Slice<byte> buf, bool all)
        {
            var trace = new StackTrace(true).ToString();
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(trace);
            int n = global::System.Math.Min(bytes.Length, buf.Len);
            for (int i = 0; i < n; i++)
                buf[i] = bytes[i];
            return n;
        }

        [GoConst]
        public static readonly string Compiler = "gc";

        public static void KeepAlive(object? obj)
        {
            System.GC.KeepAlive(obj);
        }

        public static void Goexit()
        {
            throw new ThreadInterruptedException("runtime.Goexit");
        }

        public static void LockOSThread() { }
        public static void UnlockOSThread() { }

        [return: GoReturn("error")]
        public static object? StartTrace() { return null; }
        public static Slice<byte> ReadTrace() { return new Slice<byte>(Array.Empty<byte>()); }
        public static void StopTrace() { }
        public static long MemProfileRate { get; set; } = 512 * 1024;

        public static void SetBlockProfileRate(long rate) { }
        public static long SetMutexProfileFraction(long rate) { return 0; }
        public static void SetCPUProfileRate(long hz) { }

        public static GoRuntimeFrames CallersFrames(Slice<long> callers)
        {
            return new GoRuntimeFrames(callers);
        }

        [GoFunc]
        [return: GoReturn("int", "bool")]
        public static (long, bool) BlockProfile([GoParam("[]BlockProfileRecord")] Slice<object> p) => (0, true);

        [GoFunc]
        [return: GoReturn("int", "bool")]
        public static (long, bool) MutexProfile([GoParam("[]BlockProfileRecord")] Slice<object> p) => (0, true);

        [GoFunc]
        [return: GoReturn("int", "bool")]
        public static (long, bool) ThreadCreateProfile([GoParam("[]StackRecord")] Slice<object> p) => (0, true);

        [GoFunc]
        [return: GoReturn("int", "bool")]
        public static (long, bool) GoroutineProfile([GoParam("[]StackRecord")] Slice<object> p) => (0, true);

        [GoFunc]
        [return: GoReturn("int", "bool")]
        public static (long, bool) MemProfile([GoParam("[]MemProfileRecord")] Slice<object> p, bool inuseZero) => (0, true);

        [GoFunc]
        [return: GoReturn("int64")]
        public static long NumCgoCall() => 0;
    }

    // GoRuntimeFuncType removed — annotations moved to GoRuntimeFunc.cs
}
