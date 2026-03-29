// -----------------------------------------------------------------------
// <copyright file="GoReflectMapIter.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Reflect
{
    // reflect.MapIter struct
    [GoType("struct", Name = "MapIter", Package = "reflect")]
    public class GoReflectMapIter
    {
        private System.Collections.IEnumerator? _enumerator;
        private object? _currentKey;
        private object? _currentValue;

        internal GoReflectMapIter(System.Collections.IDictionary? dict)
        {
            if (dict != null)
            {
                _enumerator = dict.GetEnumerator();
            }
        }

        public GoReflectMapIter() { }

        [GoMethod]
        public bool Next()
        {
            if (_enumerator == null)
            {
                return false;
            }
            if (_enumerator.MoveNext())
            {
                if (_enumerator.Current is System.Collections.DictionaryEntry entry)
                {
                    _currentKey = entry.Key;
                    _currentValue = entry.Value;
                }
                return true;
            }
            return false;
        }

        [GoMethod]
        public GoReflectValue Key()
        {
            return new GoReflectValue(_currentKey, new GoReflectType(_currentKey?.GetType() ?? typeof(object)));
        }

        [GoMethod]
        public GoReflectValue Value()
        {
            return new GoReflectValue(_currentValue, new GoReflectType(_currentValue?.GetType() ?? typeof(object)));
        }
    }
}
