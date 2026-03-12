// -----------------------------------------------------------------------
// <copyright file="GoOsTests.cs" company="Ziad">
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
using System.IO;
using Ngo.Runtime;
using Ngo.Runtime.Os;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class GoOsTests
{
    [TestMethod]
    public void Create_and_write_and_close()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ngo_test_{Guid.NewGuid()}.txt");
        try
        {
            var (f, err) = GoOs.Create(path);
            Assert.IsNull(err);

            var (n, writeErr) = f.WriteString("hello");
            Assert.AreEqual(5L, n);
            Assert.AreEqual("", writeErr);

            var closeErr = f.Close();
            Assert.AreEqual("", closeErr);

            Assert.AreEqual("hello", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_and_read()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ngo_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "world");
        try
        {
            var (f, err) = GoOs.Open(path);
            Assert.IsNull(err);

            var buf = new Slice<byte>(new byte[10]);
            var (n, readErr) = f.Read(buf);
            Assert.AreEqual(5, n);
            Assert.AreEqual("", readErr);

            f.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadFile_and_WriteFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ngo_test_{Guid.NewGuid()}.txt");
        try
        {
            var data = new Slice<byte>(System.Text.Encoding.UTF8.GetBytes("test data"));
            var err = GoOs.WriteFile(path, data, 0644);
            Assert.IsNull(err);

            var (readData, readErr) = GoOs.ReadFile(path);
            Assert.IsNull(readErr);
            Assert.AreEqual(9, readData.Len);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Remove_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ngo_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "to delete");

        var err = GoOs.Remove(path);
        Assert.IsNull(err);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void Getenv_and_Setenv()
    {
        var key = $"NGO_TEST_{Guid.NewGuid():N}";
        GoOs.Setenv(key, "testval");
        Assert.AreEqual("testval", GoOs.Getenv(key));
    }

    [TestMethod]
    public void Getwd_returns_directory()
    {
        var (dir, err) = GoOs.Getwd();
        Assert.IsNull(err);
        Assert.IsTrue(dir.Length > 0);
    }

    [TestMethod]
    public void Stdin_stdout_stderr_not_null()
    {
        Assert.IsNotNull(GoOs.Stdin);
        Assert.IsNotNull(GoOs.Stdout);
        Assert.IsNotNull(GoOs.Stderr);
    }

    [TestMethod]
    public void File_name()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ngo_test_{Guid.NewGuid()}.txt");
        var (f, _) = GoOs.Create(path);
        try
        {
            Assert.AreEqual(path, f.Name());
            f.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
