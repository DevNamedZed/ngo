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

using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Io
{
    /// <summary>
    /// Runtime support for Go's io package.
    /// Reader/Writer interfaces use IGoReader/IGoWriter.
    /// </summary>
    [GoPackage("io")]
    public static class GoIo
    {
        [GoVar(Type = "error")]
        public static readonly string EOF = "EOF";

        [GoVar(Type = "error")]
        public static readonly string ErrUnexpectedEOF = "unexpected EOF";

        [GoVar(Type = "error")]
        public static readonly string ErrClosedPipe = "io: read/write on closed pipe";

        [GoVar(Type = "error")]
        public static readonly string ErrShortWrite = "short write";

        [GoVar(Type = "error")]
        public static readonly string ErrNoProgress = "multiple Read calls return no data or error";

        [GoVar(Type = "error")]
        public static readonly string ErrShortBuffer = "short buffer";

        // Seek constants
        public const long SeekStart = 0;
        public const long SeekCurrent = 1;
        public const long SeekEnd = 2;

        [GoVar(Type = "io.Writer")]
        public static readonly DiscardWriter Discard = DiscardWriter.Instance;

        /// <summary>
        /// io.Copy(dst Writer, src Reader) (written int64, err error)
        /// Copies from src to dst until EOF.
        /// </summary>
        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long, string) Copy(
            [GoParam("io.Writer")] IGoWriter dst,
            [GoParam("io.Reader")] IGoReader src)
        {
            var buf = new byte[32 * 1024];
            var bufSlice = new Slice<byte>(buf);
            long written = 0;

            while (true)
            {
                var (n, readErr) = src.Read(bufSlice);
                if (n > 0)
                {
                    var toWrite = bufSlice.Reslice(0, (int)n);
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
        /// io.CopyN(dst Writer, src Reader, n int64) (written int64, err error)
        /// Copies n bytes (or until error) from src to dst.
        /// </summary>
        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long, string) CopyN(
            [GoParam("io.Writer")] IGoWriter dst,
            [GoParam("io.Reader")] IGoReader src,
            long n)
        {
            var (written, err) = Copy(dst, LimitReader(src, n));
            if (written == n)
                return (n, "");
            if (written < n && err == "")
                err = EOF;
            return (written, err);
        }

        /// <summary>
        /// io.CopyBuffer(dst Writer, src Reader, buf []byte) (written int64, err error)
        /// Like Copy but uses the provided buffer.
        /// </summary>
        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long, string) CopyBuffer(
            [GoParam("io.Writer")] IGoWriter dst,
            [GoParam("io.Reader")] IGoReader src,
            Slice<byte> buf)
        {
            // For simplicity, delegate to Copy (which uses its own buffer)
            return Copy(dst, src);
        }

        /// <summary>
        /// io.ReadAll(r Reader) ([]byte, error)
        /// Reads from r until EOF and returns all bytes.
        /// </summary>
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, string) ReadAll([GoParam("io.Reader")] IGoReader r)
        {
            var result = new List<byte>();
            var buf = new byte[512];
            var bufSlice = new Slice<byte>(buf);

            while (true)
            {
                var (n, err) = r.Read(bufSlice);
                for (long i = 0; i < n; i++)
                    result.Add(buf[i]);
                if (err == EOF)
                    return (new Slice<byte>(result.ToArray()), "");
                if (err != "")
                    return (new Slice<byte>(result.ToArray()), err);
            }
        }

        /// <summary>
        /// io.ReadFull(r Reader, buf []byte) (n int, err error)
        /// Reads exactly len(buf) bytes from r.
        /// </summary>
        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, string) ReadFull([GoParam("io.Reader")] IGoReader r, Slice<byte> buf)
        {
            return ReadAtLeast(r, buf, buf.Len);
        }

        /// <summary>
        /// io.ReadAtLeast(r Reader, buf []byte, min int) (n int, err error)
        /// Reads at least min bytes from r into buf.
        /// </summary>
        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, string) ReadAtLeast(
            [GoParam("io.Reader")] IGoReader r,
            Slice<byte> buf,
            [GoParam("int")] long min)
        {
            if (buf.Len < min)
                return (0, ErrShortBuffer);

            long totalRead = 0;
            while (totalRead < min)
            {
                var remaining = buf.Reslice((int)totalRead, buf.Len);
                var (n, err) = r.Read(remaining);
                totalRead += n;
                if (err != "")
                {
                    if (err == EOF && totalRead > 0 && totalRead < min)
                        return (totalRead, ErrUnexpectedEOF);
                    return (totalRead, err);
                }
            }
            return (totalRead, "");
        }

        /// <summary>
        /// io.WriteString(w Writer, s string) (int, error)
        /// Writes string s to w as UTF-8 bytes.
        /// </summary>
        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, string) WriteString([GoParam("io.Writer")] IGoWriter w, string s)
        {
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return (n, err);
        }

        /// <summary>
        /// io.NopCloser(r Reader) ReadCloser
        /// Returns a ReadCloser that wraps r with a no-op Close.
        /// </summary>
        [GoFunc]
        [return: GoReturn("io.ReadCloser")]
        public static NopCloserReader NopCloser([GoParam("io.Reader")] IGoReader r)
        {
            return new NopCloserReader(r);
        }

        /// <summary>
        /// io.LimitReader(r Reader, n int64) Reader
        /// Returns a Reader that reads at most n bytes from r.
        /// </summary>
        [GoFunc]
        [return: GoReturn("io.Reader")]
        public static LimitedReader LimitReader([GoParam("io.Reader")] IGoReader r, long n)
        {
            return new LimitedReader(r, n);
        }

        /// <summary>
        /// io.MultiReader(readers ...Reader) Reader
        /// Returns a Reader that concatenates the provided readers.
        /// </summary>
        [GoFunc(IsVariadic = true)]
        [return: GoReturn("io.Reader")]
        public static MultiReaderImpl MultiReader([GoParam("io.Reader")] params IGoReader[] readers)
        {
            return new MultiReaderImpl(readers);
        }

        /// <summary>
        /// io.MultiWriter(writers ...Writer) Writer
        /// Returns a Writer that writes to all provided writers.
        /// </summary>
        [GoFunc(IsVariadic = true)]
        [return: GoReturn("io.Writer")]
        public static MultiWriterImpl MultiWriter([GoParam("io.Writer")] params IGoWriter[] writers)
        {
            return new MultiWriterImpl(writers);
        }

        /// <summary>
        /// io.TeeReader(r Reader, w Writer) Reader
        /// Returns a Reader that writes to w what it reads from r.
        /// </summary>
        [GoFunc]
        [return: GoReturn("io.Reader")]
        public static TeeReaderImpl TeeReader(
            [GoParam("io.Reader")] IGoReader r,
            [GoParam("io.Writer")] IGoWriter w)
        {
            return new TeeReaderImpl(r, w);
        }

        /// <summary>
        /// io.Pipe() (*PipeReader, *PipeWriter)
        /// Creates a synchronous in-memory pipe.
        /// </summary>
        [GoFunc]
        [return: GoReturn("*io.PipeReader", "*io.PipeWriter")]
        public static (PipeReader, PipeWriter) Pipe()
        {
            var pipe = new PipeBuffer();
            return (new PipeReader(pipe), new PipeWriter(pipe));
        }

        /// <summary>
        /// io.NewSectionReader(r ReaderAt, off int64, n int64) *SectionReader
        /// Returns a SectionReader that reads from r starting at offset off for n bytes.
        /// </summary>
        [GoFunc]
        [return: GoReturn("*io.SectionReader")]
        public static SectionReader NewSectionReader(
            [GoParam("io.ReaderAt")] IGoReaderAt r,
            long off,
            long n)
        {
            return new SectionReader(r, off, n);
        }
    }
}
