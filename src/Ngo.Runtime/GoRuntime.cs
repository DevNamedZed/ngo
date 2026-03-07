// -----------------------------------------------------------------------
// <copyright file="GoRuntime.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    /// <summary>
    /// Go runtime package — stub implementations for .NET.
    /// </summary>
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
            // .NET doesn't have direct PC→function mapping like Go
            // Return a stub that provides basic info from the stack trace
            return new GoRuntimeFunc(pc);
        }

        public static long Callers(long skip, Slice<long> pc)
        {
            var trace = new StackTrace((int)skip + 1, true);
            var frames = trace.GetFrames();
            if (frames == null) return 0;

            var count = Math.Min(frames.Length, pc.Len);
            for (int i = 0; i < count; i++)
            {
                // Use a hash of method + offset as a pseudo-PC
                var frame = frames[i];
                pc[i] = frame.GetILOffset() + (frame.GetMethod()?.GetHashCode() ?? 0);
            }
            return count;
        }

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

        public static void SetFinalizer(object? obj, object? finalizer)
        {
            // No direct equivalent in .NET — GC handles finalization
        }
    }

    /// <summary>
    /// Go runtime.Func — wraps .NET stack frame info.
    /// </summary>
    public sealed class GoRuntimeFunc
    {
        private readonly long _pc;

        internal GoRuntimeFunc(long pc)
        {
            _pc = pc;
        }

        public string Name()
        {
            // Best-effort: walk the stack to find a matching frame
            return "unknown";
        }

        public long Entry()
        {
            return _pc;
        }

        public (string file, long line) FileLine(long pc)
        {
            return ("unknown", 0);
        }
    }
}
