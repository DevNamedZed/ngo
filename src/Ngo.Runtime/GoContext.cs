// -----------------------------------------------------------------------
// <copyright file="GoContext.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public sealed class GoContext
    {
        private readonly CancellationTokenSource? _cts;
        private readonly GoContext? _parent;
        private readonly Dictionary<object, object?>? _values;
        private readonly DateTimeOffset? _deadline;

        private GoContext(GoContext? parent, CancellationTokenSource? cts,
            Dictionary<object, object?>? values, DateTimeOffset? deadline)
        {
            _parent = parent;
            _cts = cts;
            _values = values;
            _deadline = deadline;
        }

        public static GoContext Background()
        {
            return new GoContext(null, null, null, null);
        }

        public static GoContext TODO()
        {
            return new GoContext(null, null, null, null);
        }

        public static (GoContext, Action) WithCancel(GoContext parent)
        {
            var cts = new CancellationTokenSource();
            var ctx = new GoContext(parent, cts, null, null);
            return (ctx, () => cts.Cancel());
        }

        public static (GoContext, Action) WithTimeout(GoContext parent, long duration)
        {
            var cts = new CancellationTokenSource();
            int ms = (int)(duration / 1_000_000);
            if (ms > 0) cts.CancelAfter(ms);
            var deadline = DateTimeOffset.UtcNow.AddMilliseconds(ms);
            var ctx = new GoContext(parent, cts, null, deadline);
            return (ctx, () => cts.Cancel());
        }

        public static (GoContext, Action) WithDeadline(GoContext parent, GoTimeValue deadline)
        {
            var cts = new CancellationTokenSource();
            var remaining = deadline.Value - DateTimeOffset.UtcNow;
            if (remaining.TotalMilliseconds > 0)
                cts.CancelAfter(remaining);
            var ctx = new GoContext(parent, cts, null, deadline.Value);
            return (ctx, () => cts.Cancel());
        }

        public static GoContext WithValue(GoContext parent, object key, object? value)
        {
            var values = new Dictionary<object, object?> { { key, value } };
            return new GoContext(parent, null, values, null);
        }

        public object? Value(object key)
        {
            if (_values != null && _values.TryGetValue(key, out var val))
                return val;
            return _parent?.Value(key);
        }

        public object? Err()
        {
            if (_cts != null && _cts.IsCancellationRequested)
                return "context canceled";
            if (_deadline.HasValue && DateTimeOffset.UtcNow > _deadline.Value)
                return "context deadline exceeded";
            return _parent?.Err();
        }

        public Channel<object> Done()
        {
            var ch = new Channel<object>(0);
            if (_cts != null)
            {
                _cts.Token.Register(() =>
                {
                    try { ch.Close(); } catch { }
                });
                if (_cts.IsCancellationRequested)
                {
                    try { ch.Close(); } catch { }
                }
            }
            return ch;
        }

        public (GoTimeValue, bool) Deadline()
        {
            if (_deadline.HasValue)
                return (new GoTimeValue(_deadline.Value), true);
            if (_parent != null)
                return _parent.Deadline();
            return (new GoTimeValue(DateTimeOffset.MinValue), false);
        }
    }
}
