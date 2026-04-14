// -----------------------------------------------------------------------
// <copyright file="ObjectFileReaderFactoryTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.ObjectFile;

namespace Ngo.Compiler.Tests.Cgo.ObjectFile;

/// <summary>
/// Unit tests for <see cref="ObjectFileReaderFactory"/>. These
/// verify both the happy path (ELF sniff returns the ELF reader)
/// and the "wrong format" diagnostics that surface specific
/// messages for Mach-O and COFF/PE rather than a generic
/// unrecognised-magic error.
/// </summary>
[TestClass]
public class ObjectFileReaderFactoryTests
{
    [TestMethod]
    public void Open_ElfFile_ReturnsElfObjectFileReader()
    {
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .WriteToTempFile();

        try
        {
            IObjectFileReader reader = ObjectFileReaderFactory.Open(path);
            Assert.IsInstanceOfType(reader, typeof(ElfObjectFileReader));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_MachO64LittleEndian_ThrowsMachOUnsupported()
    {
        byte[] machO64LittleEndianMagic = { 0xCF, 0xFA, 0xED, 0xFE };
        string path = WriteTempBytes(machO64LittleEndianMagic);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => ObjectFileReaderFactory.Open(path));
            StringAssert.Contains(thrown.Message, "Mach-O");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_MachO32BigEndian_ThrowsMachOUnsupported()
    {
        byte[] machO32BigEndianMagic = { 0xFE, 0xED, 0xFA, 0xCE };
        string path = WriteTempBytes(machO32BigEndianMagic);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => ObjectFileReaderFactory.Open(path));
            StringAssert.Contains(thrown.Message, "Mach-O");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_PeDosStub_ThrowsCoffOrPeUnsupported()
    {
        byte[] peMzMagic = { (byte)'M', (byte)'Z', 0x00, 0x00 };
        string path = WriteTempBytes(peMzMagic);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => ObjectFileReaderFactory.Open(path));
            StringAssert.Contains(thrown.Message, "COFF/PE");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_CoffAmd64Machine_ThrowsCoffOrPeUnsupported()
    {
        byte[] coffMachineMagic = { 0x64, 0x86, 0x00, 0x00 };
        string path = WriteTempBytes(coffMachineMagic);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => ObjectFileReaderFactory.Open(path));
            StringAssert.Contains(thrown.Message, "COFF/PE");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_UnknownMagic_ThrowsWithMagicBytesInMessage()
    {
        byte[] mysteryMagic = { 0xAB, 0xCD, 0xEF, 0x01 };
        string path = WriteTempBytes(mysteryMagic);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => ObjectFileReaderFactory.Open(path));
            StringAssert.Contains(thrown.Message, "0xAB");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_EmptyFile_ThrowsUnrecognised()
    {
        string path = WriteTempBytes(Array.Empty<byte>());

        try
        {
            Assert.ThrowsException<ObjectFileException>(
                () => ObjectFileReaderFactory.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Open_MissingFile_Throws()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "ngo-missing-" + Guid.NewGuid().ToString("N") + ".o");
        Assert.ThrowsException<ObjectFileException>(
            () => ObjectFileReaderFactory.Open(missingPath));
    }

    [TestMethod]
    public void Open_NullPath_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => ObjectFileReaderFactory.Open(null!));
    }

    private static string WriteTempBytes(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "ngo-magic-" + Guid.NewGuid().ToString("N") + ".o");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
