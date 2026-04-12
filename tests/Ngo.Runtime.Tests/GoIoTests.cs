// -----------------------------------------------------------------------
// <copyright file="GoIoTests.cs" company="Ziad">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Runtime;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Tests;

[TestClass]
public class GoIoTests
{
    [TestMethod]
    public void StringReader_reads_all_data()
    {
        var r = new StringReader("hello");
        var buf = new Slice<byte>(new byte[10]);
        var (n, err) = r.Read(buf);
        Assert.AreEqual(5, n);
        Assert.AreEqual(GoIo.EOF, err);
        Assert.AreEqual((byte)'h', buf[0]);
        Assert.AreEqual((byte)'o', buf[4]);
    }

    [TestMethod]
    public void StringReader_reads_in_chunks()
    {
        var r = new StringReader("hello world");
        var buf = new Slice<byte>(new byte[5]);

        var (n1, err1) = r.Read(buf);
        Assert.AreEqual(5, n1);
        Assert.AreEqual("", err1);

        var (n2, err2) = r.Read(buf);
        Assert.AreEqual(5, n2);
        Assert.AreEqual("", err2);

        var (n3, err3) = r.Read(buf);
        Assert.AreEqual(1, n3);
        Assert.AreEqual(GoIo.EOF, err3);
    }

    [TestMethod]
    public void StringReader_eof_on_empty()
    {
        var r = new StringReader("");
        var buf = new Slice<byte>(new byte[10]);
        var (n, err) = r.Read(buf);
        Assert.AreEqual(0, n);
        Assert.AreEqual(GoIo.EOF, err);
    }

    [TestMethod]
    public void DiscardWriter_discards_all()
    {
        var w = DiscardWriter.Instance;
        var data = new Slice<byte>(new byte[] { 1, 2, 3, 4, 5 });
        var (n, err) = w.Write(data);
        Assert.AreEqual(5, n);
        Assert.AreEqual("", err);
    }

    [TestMethod]
    public void ReadAll_reads_entire_stream()
    {
        var r = new StringReader("hello world");
        var (data, err) = GoIo.ReadAll(r);
        Assert.AreEqual("", err);
        Assert.AreEqual(11, data.Len);
        Assert.AreEqual((byte)'h', data[0]);
        Assert.AreEqual((byte)'d', data[10]);
    }

    [TestMethod]
    public void Copy_copies_from_reader_to_buffer()
    {
        var src = new StringReader("test data");
        var dst = new ByteBuffer();
        var (written, err) = GoIo.Copy(dst, src);
        Assert.AreEqual("", err);
        Assert.AreEqual(9, written);
        Assert.AreEqual("test data", dst.ToString());
    }

    [TestMethod]
    public void WriteString_writes_to_writer()
    {
        var dst = new ByteBuffer();
        var (n, err) = GoIo.WriteString(dst, "hello");
        Assert.AreEqual("", err);
        Assert.AreEqual(5, n);
        Assert.AreEqual("hello", dst.ToString());
    }

    /// <summary>Simple in-memory Writer for testing.</summary>
    private sealed class ByteBuffer : IGoWriter
    {
        private readonly System.Collections.Generic.List<byte> _buf = new();

        public (long, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
                _buf.Add(p[i]);
            return (p.Len, "");
        }

        public override string ToString()
        {
            return System.Text.Encoding.UTF8.GetString(_buf.ToArray());
        }
    }
}
