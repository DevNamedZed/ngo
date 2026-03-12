// -----------------------------------------------------------------------
// <copyright file="GoBufioTests.cs" company="Ziad">
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Runtime;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Tests;

[TestClass]
public class GoBufioTests
{
    [TestMethod]
    public void Scanner_reads_lines()
    {
        var r = new StringReader("hello\nworld\nfoo");
        var scanner = new GoScanner(r);

        var lines = new List<string>();
        while (scanner.Scan())
            lines.Add(scanner.Text());

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("hello", lines[0]);
        Assert.AreEqual("world", lines[1]);
        Assert.AreEqual("foo", lines[2]);
    }

    [TestMethod]
    public void Scanner_handles_empty_lines()
    {
        var r = new StringReader("a\n\nb");
        var scanner = new GoScanner(r);

        var lines = new List<string>();
        while (scanner.Scan())
            lines.Add(scanner.Text());

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("a", lines[0]);
        Assert.AreEqual("", lines[1]);
        Assert.AreEqual("b", lines[2]);
    }

    [TestMethod]
    public void Scanner_handles_trailing_newline()
    {
        var r = new StringReader("line1\nline2\n");
        var scanner = new GoScanner(r);

        var lines = new List<string>();
        while (scanner.Scan())
            lines.Add(scanner.Text());

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual("line1", lines[0]);
        Assert.AreEqual("line2", lines[1]);
    }

    [TestMethod]
    public void BufferedReader_reads_data()
    {
        var r = new StringReader("hello world");
        var br = new GoBufferedReader(r);

        var buf = new Slice<byte>(new byte[5]);
        var (n, _) = br.Read(buf);
        Assert.AreEqual(5, n);
        Assert.AreEqual((byte)'h', buf[0]);
    }

    [TestMethod]
    public void BufferedReader_readstring()
    {
        var r = new StringReader("hello\nworld\n");
        var br = new GoBufferedReader(r);

        var (line1, err1) = br.ReadString((byte)'\n');
        Assert.AreEqual("hello\n", line1);
        Assert.AreEqual("", err1);

        var (line2, err2) = br.ReadString((byte)'\n');
        Assert.AreEqual("world\n", line2);
        Assert.AreEqual("", err2);
    }

    [TestMethod]
    public void BufferedWriter_writes_and_flushes()
    {
        var buf = new TestBuffer();
        var bw = new GoBufferedWriter(buf);

        var data = new Slice<byte>(System.Text.Encoding.UTF8.GetBytes("hello"));
        bw.Write(data);
        // Not flushed yet
        Assert.AreEqual(0, buf.Data.Count);

        bw.Flush();
        Assert.AreEqual(5, buf.Data.Count);
    }

    private sealed class TestBuffer : IGoWriter
    {
        public List<byte> Data { get; } = new();

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
                Data.Add(p[i]);
            return (p.Len, "");
        }
    }
}
