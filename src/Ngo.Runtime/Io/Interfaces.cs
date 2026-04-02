// -----------------------------------------------------------------------
// <copyright file="Interfaces.cs" company="Ziad">
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

namespace Ngo.Runtime.Io
{
    /// <summary>Go io.Reader interface - Read(p []byte) (n int, err error)</summary>
    [GoType("interface", Package = "io", Name = "Reader")]
    public interface IGoReader
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        (long, string) Read(Slice<byte> p);
    }

    /// <summary>Go io.Writer interface - Write(p []byte) (n int, err error)</summary>
    [GoType("interface", Package = "io", Name = "Writer")]
    public interface IGoWriter
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        (long, string) Write(Slice<byte> p);
    }

    /// <summary>Go io.Closer interface - Close() error</summary>
    [GoType("interface", Package = "io", Name = "Closer")]
    public interface IGoCloser
    {
        [GoMethod]
        [return: GoReturn("error")]
        string Close();
    }

    /// <summary>Go io.ReaderAt interface - ReadAt(p []byte, off int64) (n int, err error)</summary>
    [GoType("interface", Package = "io", Name = "ReaderAt")]
    public interface IGoReaderAt
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        (long, string) ReadAt(Slice<byte> p, long off);
    }

    /// <summary>Go io.WriterAt interface - WriteAt(p []byte, off int64) (n int, err error)</summary>
    [GoType("interface", Package = "io", Name = "WriterAt")]
    public interface IGoWriterAt
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        (long, string) WriteAt(Slice<byte> p, long off);
    }

    /// <summary>Go io.Seeker interface - Seek(offset int64, whence int) (int64, error)</summary>
    [GoType("interface", Package = "io", Name = "Seeker")]
    public interface IGoSeeker
    {
        [GoMethod]
        [return: GoReturn("int64", "error")]
        (long, string) Seek(long offset, [GoParam("int")] long whence);
    }

    /// <summary>Go io.ReadCloser interface = Reader + Closer</summary>
    [GoType("interface", Package = "io", Name = "ReadCloser")]
    public interface IGoReadCloser : IGoReader, IGoCloser
    {
    }

    /// <summary>Go io.WriteCloser interface = Writer + Closer</summary>
    [GoType("interface", Package = "io", Name = "WriteCloser")]
    public interface IGoWriteCloser : IGoWriter, IGoCloser
    {
    }

    /// <summary>Go io.ReadWriter interface = Reader + Writer</summary>
    [GoType("interface", Package = "io", Name = "ReadWriter")]
    public interface IGoReadWriter : IGoReader, IGoWriter
    {
    }

    /// <summary>Go io.ReadWriteCloser interface = Reader + Writer + Closer</summary>
    [GoType("interface", Package = "io", Name = "ReadWriteCloser")]
    public interface IGoReadWriteCloser : IGoReader, IGoWriter, IGoCloser
    {
    }

    /// <summary>Go io.ReadSeeker interface = Reader + Seeker</summary>
    [GoType("interface", Package = "io", Name = "ReadSeeker")]
    public interface IGoReadSeeker : IGoReader, IGoSeeker
    {
    }

    /// <summary>Go io.WriteSeeker interface = Writer + Seeker</summary>
    [GoType("interface", Package = "io", Name = "WriteSeeker")]
    public interface IGoWriteSeeker : IGoWriter, IGoSeeker
    {
    }

    /// <summary>Go io.ReadWriteSeeker interface = Reader + Writer + Seeker</summary>
    [GoType("interface", Package = "io", Name = "ReadWriteSeeker")]
    public interface IGoReadWriteSeeker : IGoReader, IGoWriter, IGoSeeker
    {
    }

    /// <summary>Go io.WriterTo interface - WriteTo(w Writer) (int64, error)</summary>
    [GoType("interface", Package = "io", Name = "WriterTo")]
    public interface IGoWriterTo
    {
        [GoMethod]
        [return: GoReturn("int64", "error")]
        (long, string) WriteTo(IGoWriter w);
    }

    /// <summary>Go io.ReaderFrom interface - ReadFrom(r Reader) (int64, error)</summary>
    [GoType("interface", Package = "io", Name = "ReaderFrom")]
    public interface IGoReaderFrom
    {
        [GoMethod]
        [return: GoReturn("int64", "error")]
        (long, string) ReadFrom(IGoReader r);
    }

    /// <summary>Go io.ByteReader interface - ReadByte() (byte, error)</summary>
    [GoType("interface", Package = "io", Name = "ByteReader")]
    public interface IGoByteReader
    {
        [GoMethod]
        [return: GoReturn("byte", "error")]
        (byte, string) ReadByte();
    }

    /// <summary>Go io.ByteScanner interface = ByteReader + UnreadByte()</summary>
    [GoType("interface", Package = "io", Name = "ByteScanner")]
    public interface IGoByteScanner : IGoByteReader
    {
        [GoMethod]
        [return: GoReturn("error")]
        string UnreadByte();
    }

    /// <summary>Go io.RuneReader interface - ReadRune() (rune, int, error)</summary>
    [GoType("interface", Package = "io", Name = "RuneReader")]
    public interface IGoRuneReader
    {
        [GoMethod]
        [return: GoReturn("rune", "int", "error")]
        (long, long, string) ReadRune();
    }

    /// <summary>Go io.RuneScanner interface = RuneReader + UnreadRune()</summary>
    [GoType("interface", Package = "io", Name = "RuneScanner")]
    public interface IGoRuneScanner : IGoRuneReader
    {
        [GoMethod]
        [return: GoReturn("error")]
        string UnreadRune();
    }

    /// <summary>Go io.ByteWriter interface - WriteByte(c byte) error</summary>
    [GoType("interface", Package = "io", Name = "ByteWriter")]
    public interface IGoByteWriter
    {
        [GoMethod]
        [return: GoReturn("error")]
        string WriteByte(byte c);
    }

    /// <summary>Go io.StringWriter interface - WriteString(s string) (int, error)</summary>
    [GoType("interface", Package = "io", Name = "StringWriter")]
    public interface IGoStringWriter
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        (long, string) WriteString(string s);
    }
}
