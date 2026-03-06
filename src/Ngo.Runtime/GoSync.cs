// -----------------------------------------------------------------------
// <copyright file="GoSync.cs" company="Ziad">
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
using System.Threading;

namespace Ngo.Runtime
{
    public sealed class WaitGroup
    {
        private int _counter;
        private readonly ManualResetEventSlim _event = new(true);

        public void Add(long delta)
        {
            int newVal = Interlocked.Add(ref _counter, (int)delta);
            if (newVal < 0)
            {
                throw new GoPanicException("sync: negative WaitGroup counter");
            }
            if (newVal == 0)
            {
                _event.Set();
            }
            else
            {
                _event.Reset();
            }
        }

        public void Done()
        {
            Add(-1);
        }

        public void Wait()
        {
            _event.Wait();
        }
    }

    public sealed class Mutex
    {
        private readonly object _lock = new();

        public void Lock()
        {
            Monitor.Enter(_lock);
        }

        public void Unlock()
        {
            Monitor.Exit(_lock);
        }
    }

    public sealed class Once
    {
        private int _done;
        private readonly object _lock = new();

        public void Do(Action f)
        {
            if (Interlocked.CompareExchange(ref _done, 1, 0) == 0)
            {
                lock (_lock)
                {
                    f();
                }
            }
        }
    }

    public sealed class RWMutex
    {
        private readonly ReaderWriterLockSlim _rwlock = new();

        public void RLock() => _rwlock.EnterReadLock();

        public void RUnlock() => _rwlock.ExitReadLock();

        public void Lock() => _rwlock.EnterWriteLock();

        public void Unlock() => _rwlock.ExitWriteLock();
    }

    public sealed class SyncMap
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<object, object?> _dict = new();

        public void Store(object key, object? value)
        {
            _dict[key] = value;
        }

        public (object?, bool) Load(object key)
        {
            if (_dict.TryGetValue(key, out var value))
                return (value, true);
            return (null, false);
        }

        public void Delete(object key)
        {
            _dict.TryRemove(key, out _);
        }

        public (object?, bool) LoadOrStore(object key, object? value)
        {
            if (_dict.TryGetValue(key, out var existing))
                return (existing, true);
            _dict[key] = value;
            return (value, false);
        }

        public (object?, bool) LoadAndDelete(object key)
        {
            if (_dict.TryRemove(key, out var value))
                return (value, true);
            return (null, false);
        }

        public void Range(Func<object, object?, bool> f)
        {
            foreach (var kvp in _dict)
            {
                if (!f(kvp.Key, kvp.Value))
                    break;
            }
        }
    }
}
