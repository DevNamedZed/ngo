// -----------------------------------------------------------------------
// <copyright file="Goroutine.cs" company="Ziad">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ngo.Runtime
{
    /// <summary>
    /// Goroutine spawning. Maps go f() to Task.Run on the .NET ThreadPool.
    /// </summary>
    public static class Goroutine
    {
        private static int _activeCount;
        private static readonly ManualResetEventSlim _allDone = new(true);
        private static readonly object _lock = new();

        /// <summary>Launch a goroutine (go func() { ... }).</summary>
        public static void Go(Action action)
        {
            IncrementActive();
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (GoPanicException)
                {
                    // Unrecovered panic in goroutine crashes the goroutine, not the program.
                    // In Go, the runtime prints the panic and stack trace, then terminates.
                    // For now, silently terminate the goroutine.
                }
                finally
                {
                    DecrementActive();
                }
            });
        }

        /// <summary>Launch a goroutine with an argument (go func(x) { ... }).</summary>
        public static void Go<T>(Action<T> action, T arg)
        {
            Go(() => action(arg));
        }

        /// <summary>Wait for all spawned goroutines to complete. Called at end of main.</summary>
        public static void WaitAll()
        {
            _allDone.Wait();
        }

        /// <summary>Wait for all goroutines with a timeout. Returns true if all completed.</summary>
        public static bool WaitAll(TimeSpan timeout)
        {
            return _allDone.Wait(timeout);
        }

        /// <summary>Number of currently active goroutines (excluding main).</summary>
        public static int ActiveCount => Volatile.Read(ref _activeCount);

        private static void IncrementActive()
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _activeCount);
                _allDone.Reset();
            }
        }

        private static void DecrementActive()
        {
            lock (_lock)
            {
                if (Interlocked.Decrement(ref _activeCount) == 0)
                {
                    _allDone.Set();
                }
            }
        }
    }
}
