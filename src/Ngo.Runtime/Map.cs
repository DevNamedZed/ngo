// -----------------------------------------------------------------------
// <copyright file="Map.cs" company="Ziad">
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
using System.Collections;
using System.Collections.Generic;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go map: a hash table with reference semantics.
    /// Nil map reads return zero values; writes panic.
    /// Iteration order is randomized per Go spec.
    /// </summary>
    public class Map<K, V> : IEnumerable<KeyValuePair<K, V>> where K : notnull
    {
        private readonly Dictionary<K, V>? _data;

        /// <summary>Creates a nil map (reads return zero, writes panic).</summary>
        private Map(bool isNil)
        {
            _data = isNil ? null : new Dictionary<K, V>();
        }

        /// <summary>Creates an initialized (non-nil) empty map.</summary>
        public Map()
        {
            _data = new Dictionary<K, V>();
        }

        /// <summary>Creates an initialized map with the given capacity hint.</summary>
        public Map(int capacity)
        {
            _data = new Dictionary<K, V>(capacity);
        }

        /// <summary>Creates a nil map.</summary>
        public static Map<K, V> Nil() => new Map<K, V>(isNil: true);

        /// <summary>True if this is a nil map.</summary>
        public bool IsNil => _data == null;

        /// <summary>Number of key-value pairs.</summary>
        public int Len => _data?.Count ?? 0;

        /// <summary>
        /// Index access. Read: returns zero value for missing keys.
        /// Write: panics on nil map.
        /// </summary>
        public V this[K key]
        {
            get
            {
                if (_data == null) return default!;
                return _data.TryGetValue(key, out var val) ? val : default!;
            }
            set
            {
                if (_data == null)
                    throw new GoPanicException("assignment to entry in nil map");
                _data[key] = value;
            }
        }

        /// <summary>Two-value lookup: v, ok := m[key]</summary>
        public (V value, bool ok) Get(K key)
        {
            if (_data == null) return (default!, false);
            if (_data.TryGetValue(key, out var val)) return (val, true);
            return (default!, false);
        }

        /// <summary>Set a key-value pair. Panics on nil map.</summary>
        public void Set(K key, V value)
        {
            if (_data == null)
                throw new GoPanicException("assignment to entry in nil map");
            _data[key] = value;
        }

        /// <summary>Delete a key. No-op on nil map or missing key.</summary>
        public void Delete(K key)
        {
            _data?.Remove(key);
        }

        /// <summary>Check if key exists.</summary>
        public bool ContainsKey(K key)
        {
            return _data != null && _data.ContainsKey(key);
        }

        /// <summary>
        /// Range iteration with randomized order (Go spec requires non-deterministic iteration).
        /// </summary>
        public IEnumerable<(K key, V value)> Range()
        {
            if (_data == null) yield break;

            // Randomize order by shuffling keys
            var keys = new List<K>(_data.Keys);
            Shuffle(keys);

            foreach (var key in keys)
            {
                // Key may have been deleted during iteration — skip
                if (_data.TryGetValue(key, out var val))
                {
                    yield return (key, val);
                }
            }
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            if (_data == null) yield break;
            foreach (var kvp in _data) yield return kvp;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static void Shuffle(List<K> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
