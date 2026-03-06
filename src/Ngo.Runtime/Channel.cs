// -----------------------------------------------------------------------
// <copyright file="Channel.cs" company="Ziad">
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

using System.Collections.Generic;
using System.Threading;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go channel: synchronization primitive for goroutine communication.
    /// Unbuffered channels enforce rendezvous (sender blocks until receiver is ready).
    /// Buffered channels allow sends until the buffer is full.
    /// </summary>
    public sealed class Channel<T>
    {
        private readonly Queue<T> _buffer;
        private readonly int _capacity;
        private readonly object _lock = new();
        private bool _closed;

        // Waiters for rendezvous (unbuffered) and overflow (buffered full)
        private readonly Queue<SenderWaiter> _senderWaiters = new();
        private readonly Queue<ReceiverWaiter> _receiverWaiters = new();

        /// <summary>Creates an unbuffered channel.</summary>
        public Channel() : this(0) { }

        /// <summary>Creates a buffered channel with the given capacity.</summary>
        public Channel(int capacity)
        {
            _capacity = capacity;
            _buffer = new Queue<T>(capacity > 0 ? capacity : 1);
        }

        /// <summary>True if the channel has been closed.</summary>
        public bool IsClosed
        {
            get { lock (_lock) return _closed; }
        }

        /// <summary>Number of items currently buffered.</summary>
        public int Length
        {
            get { lock (_lock) return _buffer.Count; }
        }

        /// <summary>Buffer capacity (0 = unbuffered).</summary>
        public int Capacity => _capacity;

        /// <summary>Blocking send. Panics if channel is closed.</summary>
        public void Send(T value)
        {
            ManualResetEventSlim? wait = null;

            lock (_lock)
            {
                if (_closed)
                    throw new GoPanicException("send on closed channel");

                // Try to hand off directly to a waiting receiver
                while (_receiverWaiters.Count > 0)
                {
                    var receiver = _receiverWaiters.Dequeue();
                    if (!receiver.Done)
                    {
                        receiver.Value = value;
                        receiver.Ok = true;
                        receiver.Done = true;
                        receiver.Event.Set();
                        return;
                    }
                }

                // Buffered: try to enqueue
                if (_capacity > 0 && _buffer.Count < _capacity)
                {
                    _buffer.Enqueue(value);
                    return;
                }

                // Must wait — register as sender waiter
                var waiter = new SenderWaiter { Value = value, Event = new ManualResetEventSlim() };
                _senderWaiters.Enqueue(waiter);
                wait = waiter.Event;
            }

            wait.Wait();
            wait.Dispose();
        }

        /// <summary>Blocking receive. Returns (value, true) or (default, false) if closed and empty.</summary>
        public (T value, bool ok) Receive()
        {
            ManualResetEventSlim? wait = null;
            ReceiverWaiter? waiter = null;

            lock (_lock)
            {
                // Try to dequeue from buffer
                if (_buffer.Count > 0)
                {
                    var val = _buffer.Dequeue();

                    // Unblock a waiting sender if any
                    while (_senderWaiters.Count > 0)
                    {
                        var sender = _senderWaiters.Dequeue();
                        if (!sender.Done)
                        {
                            _buffer.Enqueue(sender.Value);
                            sender.Done = true;
                            sender.Event.Set();
                            break;
                        }
                    }

                    return (val, true);
                }

                // Try to receive from a waiting sender (unbuffered rendezvous)
                while (_senderWaiters.Count > 0)
                {
                    var sender = _senderWaiters.Dequeue();
                    if (!sender.Done)
                    {
                        var val = sender.Value;
                        sender.Done = true;
                        sender.Event.Set();
                        return (val, true);
                    }
                }

                // Closed and empty
                if (_closed)
                    return (default!, false);

                // Must wait — register as receiver waiter
                waiter = new ReceiverWaiter { Event = new ManualResetEventSlim() };
                _receiverWaiters.Enqueue(waiter);
                wait = waiter.Event;
            }

            wait.Wait();
            wait.Dispose();
            return (waiter.Value, waiter.Ok);
        }

        /// <summary>Non-blocking send. Returns true if the value was sent.</summary>
        public bool TrySend(T value)
        {
            lock (_lock)
            {
                if (_closed)
                    throw new GoPanicException("send on closed channel");

                // Try to hand off directly to a waiting receiver
                while (_receiverWaiters.Count > 0)
                {
                    var receiver = _receiverWaiters.Dequeue();
                    if (!receiver.Done)
                    {
                        receiver.Value = value;
                        receiver.Ok = true;
                        receiver.Done = true;
                        receiver.Event.Set();
                        return true;
                    }
                }

                // Buffered: try to enqueue
                if (_capacity > 0 && _buffer.Count < _capacity)
                {
                    _buffer.Enqueue(value);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Non-blocking receive. Returns (value, ok, completed).
        /// completed=true means the operation finished (value is valid if ok=true, channel closed if ok=false).
        /// completed=false means the operation would block.
        /// </summary>
        public (T value, bool ok, bool completed) TryReceive()
        {
            lock (_lock)
            {
                // Try to dequeue from buffer
                if (_buffer.Count > 0)
                {
                    var val = _buffer.Dequeue();

                    // Unblock a waiting sender if any
                    while (_senderWaiters.Count > 0)
                    {
                        var sender = _senderWaiters.Dequeue();
                        if (!sender.Done)
                        {
                            _buffer.Enqueue(sender.Value);
                            sender.Done = true;
                            sender.Event.Set();
                            break;
                        }
                    }

                    return (val, true, true);
                }

                // Try to receive from a waiting sender (unbuffered rendezvous)
                while (_senderWaiters.Count > 0)
                {
                    var sender = _senderWaiters.Dequeue();
                    if (!sender.Done)
                    {
                        var val = sender.Value;
                        sender.Done = true;
                        sender.Event.Set();
                        return (val, true, true);
                    }
                }

                // Closed and empty
                if (_closed)
                    return (default!, false, true);

                return (default!, false, false);
            }
        }

        /// <summary>Close the channel. Panics if already closed.</summary>
        public void Close()
        {
            lock (_lock)
            {
                if (_closed)
                    throw new GoPanicException("close of closed channel");
                _closed = true;

                // Wake up all waiting receivers with zero value + false
                while (_receiverWaiters.Count > 0)
                {
                    var receiver = _receiverWaiters.Dequeue();
                    if (!receiver.Done)
                    {
                        receiver.Value = default!;
                        receiver.Ok = false;
                        receiver.Done = true;
                        receiver.Event.Set();
                    }
                }

                // Wake up all waiting senders — they'll panic
                while (_senderWaiters.Count > 0)
                {
                    var sender = _senderWaiters.Dequeue();
                    if (!sender.Done)
                    {
                        sender.Done = true;
                        sender.Event.Set();
                        // Sender will panic when they check after waking
                    }
                }
            }
        }

        private class SenderWaiter
        {
            public T Value;
            public bool Done;
            public ManualResetEventSlim Event = null!;
        }

        private class ReceiverWaiter
        {
            public T Value = default!;
            public bool Ok;
            public bool Done;
            public ManualResetEventSlim Event = null!;
        }
    }
}
