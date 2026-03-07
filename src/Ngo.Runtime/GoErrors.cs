// -----------------------------------------------------------------------
// <copyright file="GoErrors.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoErrors
    {
        public static object New(string text) => text;

        public static object? Unwrap(object? err)
        {
            if (err is WrappedError w) return w.Inner;
            return null;
        }

        public static bool Is(object? err, object? target)
        {
            while (err != null)
            {
                if (Equals(err, target)) return true;
                if (err is string s1 && target is string s2 && s1 == s2) return true;
                if (err is WrappedError w) { err = w.Inner; continue; }
                break;
            }
            return false;
        }

        public static bool As(object? err, object? target)
        {
            // In Go, errors.As checks if err (or any in its chain) matches the type pointed to by target.
            // Since we don't have typed errors with pointer semantics, this is a simplified version:
            // returns true if err is assignable to target's type.
            if (err == null || target == null) return false;
            var targetType = target.GetType();
            while (err != null)
            {
                if (targetType.IsInstanceOfType(err)) return true;
                if (err is WrappedError w) { err = w.Inner; continue; }
                break;
            }
            return false;
        }

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

    public sealed class JoinedError
    {
        private readonly object[] _errors;

        public JoinedError(object[] errors)
        {
            _errors = errors;
        }

        public object[] Unwrap() => _errors;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _errors.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_errors[i]);
            }
            return sb.ToString();
        }
    }

    public sealed class WrappedError
    {
        public string Message { get; }
        public object? Inner { get; }

        public WrappedError(string message, object? inner)
        {
            Message = message;
            Inner = inner;
        }

        public override string ToString() => Message;
    }
}
