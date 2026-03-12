using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "Map", Package = "sync")]
    public sealed class Map
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<object, object?> _dict = new();

        [GoMethod]
        public void Store(object key, object? value)
        {
            _dict[key] = value;
        }

        [GoMethod]
        [return: GoReturn("interface{}", "bool")]
        public (object?, bool) Load(object key)
        {
            if (_dict.TryGetValue(key, out var value))
                return (value, true);
            return (null, false);
        }

        [GoMethod]
        public void Delete(object key)
        {
            _dict.TryRemove(key, out _);
        }

        [GoMethod]
        [return: GoReturn("interface{}", "bool")]
        public (object?, bool) LoadOrStore(object key, object? value)
        {
            if (_dict.TryGetValue(key, out var existing))
                return (existing, true);
            _dict[key] = value;
            return (value, false);
        }

        [GoMethod]
        [return: GoReturn("interface{}", "bool")]
        public (object?, bool) LoadAndDelete(object key)
        {
            if (_dict.TryRemove(key, out var value))
                return (value, true);
            return (null, false);
        }

        [GoMethod]
        public void Range([GoParam("func(key, value interface{}) bool")] Func<object, object?, bool> f)
        {
            foreach (var kvp in _dict)
            {
                if (!f(kvp.Key, kvp.Value))
                    break;
            }
        }
    }
}
