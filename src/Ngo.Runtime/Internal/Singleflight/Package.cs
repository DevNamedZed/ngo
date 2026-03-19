using System.Collections.Generic;
using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Singleflight
{
    /// <summary>
    /// internal/singleflight — request deduplication.
    /// Ensures only one call per key runs at a time; other callers wait for the result.
    /// Used by net.Resolver for DNS lookups.
    /// </summary>
    [GoPackage("internal/singleflight")]
    public static class Package { }

    [GoType("struct", Name = "Group", Package = "internal/singleflight")]
    public class GoGroup
    {
        private readonly Dictionary<string, GoCall> _calls = new();
        private readonly object _mu = new();

        [GoType("struct", Name = "Result", Package = "internal/singleflight")]
        public class GoResult
        {
            [GoField(Name = "Val")] public object? Val;
            [GoField(Name = "Err")] public object? Err;
            [GoField(Name = "Shared")] public bool Shared;
        }

        [GoMethod]
        [return: GoReturn("interface{}", "error", "bool")]
        public (object?, object?, bool) Do(string key, object? fn)
        {
            lock (_mu)
            {
                if (_calls.TryGetValue(key, out var call))
                {
                    call.Waiters++;
                    Monitor.Wait(_mu);
                    return (call.Val, call.Err, true);
                }
                var c = new GoCall();
                _calls[key] = c;
            }

            // Execute fn — simplified: just return nil
            // Real implementation would invoke the function
            lock (_mu)
            {
                _calls.Remove(key);
                Monitor.PulseAll(_mu);
            }
            return (null, null, false);
        }

        [GoMethod]
        [return: GoReturn("<-chan Result")]
        public object? DoChan(string key, object? fn)
        {
            // Simplified: run synchronously and return result via channel
            var (val, err, shared) = Do(key, fn);
            var ch = new Channel<GoResult>(1);
            ch.TrySend(new GoResult { Val = val, Err = err, Shared = shared });
            return ch;
        }

        [GoMethod]
        public void ForgetUnshared(string key)
        {
            lock (_mu) { _calls.Remove(key); }
        }
    }

    internal class GoCall
    {
        public object? Val;
        public object? Err;
        public int Waiters;
    }
}
