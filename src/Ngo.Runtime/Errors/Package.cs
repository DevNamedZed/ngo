// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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

namespace Ngo.Runtime.Errors
{
    [GoPackage("errors")]
    public static class Package
    {
        [GoVar(Type = "error")]
        public static readonly object? ErrUnsupported = "unsupported operation";

        [GoFunc]
        [return: GoReturn("error")]
        public static object New([GoParam("string")] string text) => text;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unwrap([GoParam("error")] object? err)
        {
            if (err is WrappedError w) return w.Inner;
            return TryCallUnwrap(err);
        }

        [GoFunc]
        public static bool Is([GoParam("interface{}")] object? err, [GoParam("interface{}")] object? target)
        {
            while (err != null)
            {
                if (Equals(err, target)) return true;
                if (err is string s1 && target is string s2 && s1 == s2) return true;

                // Unwrap via wrapper's _value field (interface wrapper holds concrete error)
                var unwrapped = TryUnwrapError(err);
                if (unwrapped != null)
                {
                    err = unwrapped;
                    continue;
                }
                break;
            }
            return false;
        }

        [GoFunc]
        public static bool As([GoParam("interface{}")] object? err, [GoParam("interface{}")] object? target)
        {
            if (err == null || target == null) return false;
            var targetType = target.GetType();
            while (err != null)
            {
                if (targetType.IsInstanceOfType(err)) return true;
                // Also check the wrapped value inside interface wrappers
                var innerValue = TryGetWrappedValue(err);
                if (innerValue != null && targetType.IsInstanceOfType(innerValue))
                {
                    return true;
                }
                var unwrapped = TryUnwrapError(err);
                if (unwrapped != null)
                {
                    err = unwrapped;
                    continue;
                }
                break;
            }
            return false;
        }

        private static object? TryUnwrapError(object? err)
        {
            if (err is WrappedError w) return w.Inner;

            // Check for Unwrap() method via reflection (Go interface pattern)
            var unwrapResult = TryCallUnwrap(err);
            if (unwrapResult != null) return unwrapResult;

            // Check wrapper's _value field for Unwrap
            var innerValue = TryGetWrappedValue(err);
            if (innerValue != null)
            {
                return TryCallUnwrap(innerValue);
            }

            return null;
        }

        private static object? TryCallUnwrap(object? err)
        {
            if (err == null) return null;
            var unwrapMethod = err.GetType().GetMethod("Unwrap", System.Type.EmptyTypes);
            if (unwrapMethod != null && unwrapMethod.ReturnType != typeof(void))
            {
                return unwrapMethod.Invoke(err, null);
            }
            return null;
        }

        private static object? TryGetWrappedValue(object? obj)
        {
            if (obj == null) return null;
            var valueField = obj.GetType().GetField("_value");
            if (valueField != null)
            {
                return valueField.GetValue(obj);
            }
            return null;
        }

        [GoFunc(IsVariadic = true)]
        [return: GoReturn("error")]
        public static object? Join(params object?[] errs)
        {
            var nonNull = new System.Collections.Generic.List<object>();
            foreach (var e in errs)
            {
                if (e != null) nonNull.Add(e);
            }
            if (nonNull.Count == 0) return null;
            if (nonNull.Count == 1) return nonNull[0];
            return new JoinedError(nonNull.ToArray());
        }
    }
}
