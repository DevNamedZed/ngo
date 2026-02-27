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
}
