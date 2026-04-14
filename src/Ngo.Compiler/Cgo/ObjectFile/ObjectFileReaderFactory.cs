// -----------------------------------------------------------------------
// <copyright file="ObjectFileReaderFactory.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// Picks the right <see cref="IObjectFileReader"/> for an object
    /// file by inspecting its magic bytes. Recognises ELF and surfaces
    /// a clear "not supported in this stage" diagnostic for Mach-O
    /// and COFF/PE so a caller that hands us a wrong-container file
    /// gets a message pointing at the format rather than a
    /// generic parse failure. Anything else throws with the raw magic
    /// bytes in the message for quick forensics.
    /// </summary>
    public static class ObjectFileReaderFactory
    {
        private const int MagicByteCount = 4;

        public static IObjectFileReader Open(string objectFilePath)
        {
            if (objectFilePath == null)
            {
                throw new ArgumentNullException(nameof(objectFilePath));
            }

            byte[] magicBytes = ReadLeadingBytes(objectFilePath, MagicByteCount);

            if (IsElfMagic(magicBytes))
            {
                return new ElfObjectFileReader();
            }
            if (IsMachO32Magic(magicBytes) || IsMachO64Magic(magicBytes) || IsMachOFatMagic(magicBytes))
            {
                throw new ObjectFileException(
                    "Mach-O object files are not supported in this stage; " +
                    "only ELF64 is implemented.",
                    objectFilePath);
            }
            if (IsCoffOrPeMagic(magicBytes))
            {
                throw new ObjectFileException(
                    "COFF/PE object files are not supported in this stage; " +
                    "only ELF64 is implemented.",
                    objectFilePath);
            }

            throw new ObjectFileException(
                "Unrecognised object file magic: " + FormatMagicBytes(magicBytes) + ".",
                objectFilePath);
        }

        private static byte[] ReadLeadingBytes(string objectFilePath, int byteCount)
        {
            try
            {
                using FileStream stream = File.OpenRead(objectFilePath);
                byte[] buffer = new byte[byteCount];
                int totalRead = 0;
                while (totalRead < byteCount)
                {
                    int readThisCall = stream.Read(buffer, totalRead, byteCount - totalRead);
                    if (readThisCall == 0)
                    {
                        byte[] truncated = new byte[totalRead];
                        Array.Copy(buffer, truncated, totalRead);
                        return truncated;
                    }
                    totalRead += readThisCall;
                }
                return buffer;
            }
            catch (IOException ioException)
            {
                throw new ObjectFileException(
                    "Failed to read object file header: " + ioException.Message,
                    objectFilePath,
                    ioException);
            }
        }

        private static bool IsElfMagic(byte[] magicBytes)
        {
            return magicBytes.Length >= 4
                && magicBytes[0] == 0x7F
                && magicBytes[1] == (byte)'E'
                && magicBytes[2] == (byte)'L'
                && magicBytes[3] == (byte)'F';
        }

        private static bool IsMachO32Magic(byte[] magicBytes)
        {
            return Matches(magicBytes, 0xFE, 0xED, 0xFA, 0xCE)
                || Matches(magicBytes, 0xCE, 0xFA, 0xED, 0xFE);
        }

        private static bool IsMachO64Magic(byte[] magicBytes)
        {
            return Matches(magicBytes, 0xFE, 0xED, 0xFA, 0xCF)
                || Matches(magicBytes, 0xCF, 0xFA, 0xED, 0xFE);
        }

        private static bool IsMachOFatMagic(byte[] magicBytes)
        {
            return Matches(magicBytes, 0xCA, 0xFE, 0xBA, 0xBE)
                || Matches(magicBytes, 0xBE, 0xBA, 0xFE, 0xCA);
        }

        private static bool IsCoffOrPeMagic(byte[] magicBytes)
        {
            if (magicBytes.Length < 2)
            {
                return false;
            }
            if (magicBytes[0] == (byte)'M' && magicBytes[1] == (byte)'Z')
            {
                return true;
            }
            ushort magicAsMachineType = (ushort)(magicBytes[0] | (magicBytes[1] << 8));
            return magicAsMachineType == 0x8664                                       // IMAGE_FILE_MACHINE_AMD64
                || magicAsMachineType == 0x014C                                       // IMAGE_FILE_MACHINE_I386
                || magicAsMachineType == 0xAA64;                                      // IMAGE_FILE_MACHINE_ARM64
        }

        private static bool Matches(byte[] magicBytes, byte b0, byte b1, byte b2, byte b3)
        {
            return magicBytes.Length >= 4
                && magicBytes[0] == b0
                && magicBytes[1] == b1
                && magicBytes[2] == b2
                && magicBytes[3] == b3;
        }

        private static string FormatMagicBytes(byte[] magicBytes)
        {
            string[] parts = new string[magicBytes.Length];
            for (int index = 0; index < magicBytes.Length; index++)
            {
                parts[index] = "0x" + magicBytes[index].ToString("X2");
            }
            return string.Join(" ", parts);
        }
    }
}
