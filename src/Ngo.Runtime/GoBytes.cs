// -----------------------------------------------------------------------
// <copyright file="GoBytes.cs" company="Ziad">
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
using System.Text;

namespace Ngo.Runtime
{
    public static class GoBytes
    {
        public static bool Contains(Slice<byte> b, Slice<byte> subslice)
        {
            return Index(b, subslice) >= 0;
        }

        public static bool HasPrefix(Slice<byte> s, Slice<byte> prefix)
        {
            if (prefix.Len > s.Len) return false;
            for (int i = 0; i < prefix.Len; i++)
            {
                if (s[i] != prefix[i]) return false;
            }
            return true;
        }

        public static bool HasSuffix(Slice<byte> s, Slice<byte> suffix)
        {
            if (suffix.Len > s.Len) return false;
            int offset = s.Len - suffix.Len;
            for (int i = 0; i < suffix.Len; i++)
            {
                if (s[offset + i] != suffix[i]) return false;
            }
            return true;
        }

        public static long Index(Slice<byte> s, Slice<byte> sep)
        {
            if (sep.Len == 0) return 0;
            if (sep.Len > s.Len) return -1;

            for (int i = 0; i <= s.Len - sep.Len; i++)
            {
                bool match = true;
                for (int j = 0; j < sep.Len; j++)
                {
                    if (s[i + j] != sep[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        public static bool Equal(Slice<byte> a, Slice<byte> b)
        {
            if (a.Len != b.Len) return false;
            for (int i = 0; i < a.Len; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        public static long Compare(Slice<byte> a, Slice<byte> b)
        {
            int minLen = Math.Min(a.Len, b.Len);
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            if (a.Len < b.Len) return -1;
            if (a.Len > b.Len) return 1;
            return 0;
        }

        public static Slice<byte> Repeat(Slice<byte> b, long count)
        {
            var result = new byte[b.Len * (int)count];
            int pos = 0;
            for (long c = 0; c < count; c++)
            {
                for (int i = 0; i < b.Len; i++)
                    result[pos++] = b[i];
            }
            return new Slice<byte>(result);
        }

        public static Slice<byte> ToUpper(Slice<byte> s)
        {
            var str = Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(Encoding.UTF8.GetBytes(str.ToUpperInvariant()));
        }

        public static Slice<byte> ToLower(Slice<byte> s)
        {
            var str = Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(Encoding.UTF8.GetBytes(str.ToLowerInvariant()));
        }

        public static Slice<byte> TrimSpace(Slice<byte> s)
        {
            var str = Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(Encoding.UTF8.GetBytes(str.Trim()));
        }

        public static Slice<byte> ReplaceAll(Slice<byte> s, Slice<byte> old, Slice<byte> @new)
        {
            var str = Encoding.UTF8.GetString(SliceToArray(s));
            var oldStr = Encoding.UTF8.GetString(SliceToArray(old));
            var newStr = Encoding.UTF8.GetString(SliceToArray(@new));
            str = str.Replace(oldStr, newStr);
            return new Slice<byte>(Encoding.UTF8.GetBytes(str));
        }

        public static GoBuffer NewBuffer(Slice<byte> buf)
        {
            var b = new GoBuffer();
            b.Write(buf);
            return b;
        }

        private static byte[] SliceToArray(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
                arr[i] = s[i];
            return arr;
        }
    }

    public sealed class GoBuffer : IGoReader, IGoWriter
    {
        private byte[] _buf = new byte[64];
        private int _len;

        public (int, string) Write(Slice<byte> p)
        {
            EnsureCapacity(_len + p.Len);
            for (int i = 0; i < p.Len; i++)
                _buf[_len++] = p[i];
            return (p.Len, "");
        }

        public (long, object?) WriteString(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            EnsureCapacity(_len + bytes.Length);
            Array.Copy(bytes, 0, _buf, _len, bytes.Length);
            _len += bytes.Length;
            return (bytes.Length, null);
        }

        public object? WriteByte(long c)
        {
            EnsureCapacity(_len + 1);
            _buf[_len++] = (byte)c;
            return null;
        }

        public (long, object?) ReadFrom(object reader)
        {
            if (reader is IGoReader r)
            {
                long total = 0;
                var tmp = new Slice<byte>(new byte[4096]);
                while (true)
                {
                    var (n, err) = r.Read(tmp);
                    if (n > 0)
                    {
                        EnsureCapacity(_len + n);
                        for (int i = 0; i < n; i++)
                            _buf[_len++] = tmp[i];
                        total += n;
                    }
                    if (err is string s && s == GoIo.EOF)
                        break;
                    if (err != null && err is string se && se != "")
                        return (total, err);
                    if (n == 0)
                        break;
                }
                return (total, null);
            }
            return (0, "bytes.Buffer: ReadFrom: invalid reader");
        }

        public (int, string) Read(Slice<byte> p)
        {
            if (_len == 0) return (0, GoIo.EOF);
            int n = Math.Min(p.Len, _len);
            for (int i = 0; i < n; i++)
                p[i] = _buf[i];
            // Shift remaining bytes
            Array.Copy(_buf, n, _buf, 0, _len - n);
            _len -= n;
            return (n, "");
        }

        public Slice<byte> Bytes()
        {
            var result = new byte[_len];
            Array.Copy(_buf, result, _len);
            return new Slice<byte>(result);
        }

        public string String()
        {
            return Encoding.UTF8.GetString(_buf, 0, _len);
        }

        public long Len() => _len;

        public long Cap() => _buf.Length;

        public void Reset()
        {
            _len = 0;
        }

        public override string ToString() => String();

        private void EnsureCapacity(int needed)
        {
            if (needed <= _buf.Length) return;
            int newCap = _buf.Length * 2;
            if (newCap < needed) newCap = needed;
            var newBuf = new byte[newCap];
            Array.Copy(_buf, newBuf, _len);
            _buf = newBuf;
        }
    }
}
