// -----------------------------------------------------------------------
// <copyright file="GoIo.cs" company="Ziad">
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
using System.Text;

namespace Ngo.Runtime
{
    /// <summary>
    /// Runtime support for Go's io package.
    /// Reader/Writer interfaces use IGoReader/IGoWriter.
    /// </summary>
    public static class GoIo
    {
        public static readonly string EOF = "EOF";

        /// <summary>
        /// io.Copy(dst Writer, src Reader) (written int64, err error)
        /// Copies from src to dst until EOF.
        /// </summary>
        public static (long, string) Copy(IGoWriter dst, IGoReader src)
        {
            var buf = new byte[32 * 1024];
            var bufSlice = new Slice<byte>(buf);
            long written = 0;

            while (true)
            {
                var (n, readErr) = src.Read(bufSlice);
                if (n > 0)
                {
                    var toWrite = bufSlice.Reslice(0, n);
                    var (nw, writeErr) = dst.Write(toWrite);
                    written += nw;
                    if (writeErr != "")
                        return (written, writeErr);
                    if (nw != n)
                        return (written, "short write");
                }
                if (readErr == EOF)
                    return (written, "");
                if (readErr != "")
                    return (written, readErr);
            }
        }

        /// <summary>
        /// io.ReadAll(r Reader) ([]byte, error)
        /// Reads from r until EOF and returns all bytes.
        /// </summary>
        public static (Slice<byte>, string) ReadAll(IGoReader r)
        {
            var result = new List<byte>();
            var buf = new byte[512];
            var bufSlice = new Slice<byte>(buf);

            while (true)
            {
                var (n, err) = r.Read(bufSlice);
                for (int i = 0; i < n; i++)
                    result.Add(buf[i]);
                if (err == EOF)
                    return (new Slice<byte>(result.ToArray()), "");
                if (err != "")
                    return (new Slice<byte>(result.ToArray()), err);
            }
        }

        /// <summary>
        /// io.WriteString(w Writer, s string) (int, error)
        /// Writes string s to w as UTF-8 bytes.
        /// </summary>
        public static (long, string) WriteString(IGoWriter w, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return ((long)n, err);
        }
    }

    /// <summary>Go io.Reader interface — Read(p []byte) (n int, err error)</summary>
    public interface IGoReader
    {
        (int, string) Read(Slice<byte> p);
    }

    /// <summary>Go io.Writer interface — Write(p []byte) (n int, err error)</summary>
    public interface IGoWriter
    {
        (int, string) Write(Slice<byte> p);
    }

    /// <summary>Go io.Closer interface — Close() error</summary>
    public interface IGoCloser
    {
        string Close();
    }

    /// <summary>An io.Writer that discards all data (io.Discard).</summary>
    public sealed class DiscardWriter : IGoWriter
    {
        public static readonly DiscardWriter Instance = new DiscardWriter();

        public (int, string) Write(Slice<byte> p)
        {
            return (p.Len, "");
        }
    }

    /// <summary>A Reader that reads from a string (like strings.NewReader).</summary>
    public sealed class StringReader : IGoReader
    {
        private readonly byte[] _data;
        private int _pos;

        public StringReader(string s)
        {
            _data = Encoding.UTF8.GetBytes(s);
            _pos = 0;
        }

        public (int, string) Read(Slice<byte> p)
        {
            if (_pos >= _data.Length)
                return (0, GoIo.EOF);

            int n = Math.Min(p.Len, _data.Length - _pos);
            for (int i = 0; i < n; i++)
                p[i] = _data[_pos + i];
            _pos += n;

            string err = _pos >= _data.Length ? GoIo.EOF : "";
            return (n, err);
        }
    }
}
