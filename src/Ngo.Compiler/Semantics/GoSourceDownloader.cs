// -----------------------------------------------------------------------
// <copyright file="GoSourceDownloader.cs" company="Ziad">
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
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace Ngo.Compiler.Semantics
{
    internal static class GoSourceDownloader
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ngo", "gosrc");

        private static readonly HttpClient HttpClient = new();

        private static readonly Dictionary<int, string> KnownVersions = new()
        {
            { 22, "go1.22.6" },
            { 23, "go1.23.6" },
        };

        public static string? EnsureGoSource(int goVersion)
        {
            if (!KnownVersions.TryGetValue(goVersion, out var downloadVersion))
            {
                Console.Error.WriteLine($"GoSourceDownloader: unknown Go version {goVersion}");
                return null;
            }

            var sourceDirectory = Path.Combine(CacheDirectory, downloadVersion, "src");

            if (Directory.Exists(sourceDirectory) &&
                Directory.GetDirectories(sourceDirectory).Length > 0)
            {
                return sourceDirectory;
            }

            var versionDirectory = Path.Combine(CacheDirectory, downloadVersion);
            Directory.CreateDirectory(versionDirectory);

            var downloadUrl = $"https://go.dev/dl/{downloadVersion}.src.tar.gz";
            Console.Error.WriteLine($"GoSourceDownloader: downloading {downloadUrl}...");

            byte[] compressedBytes;
            try
            {
                compressedBytes = HttpClient.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"GoSourceDownloader: download failed — {exception.Message}");
                return null;
            }

            Console.Error.WriteLine($"GoSourceDownloader: extracting ({compressedBytes.Length / 1024 / 1024}MB)...");

            try
            {
                using var gzipStream = new GZipStream(
                    new MemoryStream(compressedBytes), CompressionMode.Decompress);
                using var tarStream = new MemoryStream();
                gzipStream.CopyTo(tarStream);
                tarStream.Position = 0;

                ExtractTar(tarStream, versionDirectory);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"GoSourceDownloader: extraction failed — {exception.Message}");
                return null;
            }

            if (!Directory.Exists(sourceDirectory))
            {
                Console.Error.WriteLine(
                    $"GoSourceDownloader: extraction completed but {sourceDirectory} not found");
                return null;
            }

            Console.Error.WriteLine($"GoSourceDownloader: cached at {versionDirectory}");
            return sourceDirectory;
        }

        private static void ExtractTar(Stream tarStream, string outputDirectory)
        {
            var headerBuffer = new byte[512];

            while (true)
            {
                int bytesRead = ReadFull(tarStream, headerBuffer, 0, 512);
                if (bytesRead < 512)
                {
                    break;
                }

                bool allZeroes = true;
                for (int index = 0; index < headerBuffer.Length; index++)
                {
                    if (headerBuffer[index] != 0)
                    {
                        allZeroes = false;
                        break;
                    }
                }

                if (allZeroes)
                {
                    break;
                }

                var entryName = ReadTarString(headerBuffer, 0, 100);
                var sizeField = ReadTarString(headerBuffer, 124, 12);
                var typeFlag = (char)headerBuffer[156];
                var namePrefix = ReadTarString(headerBuffer, 345, 155);

                if (!string.IsNullOrEmpty(namePrefix))
                {
                    entryName = namePrefix + "/" + entryName;
                }

                long entrySize = 0;
                if (!string.IsNullOrEmpty(sizeField))
                {
                    try
                    {
                        entrySize = Convert.ToInt64(sizeField.Trim(), 8);
                    }
                    catch (FormatException)
                    {
                    }
                }

                bool isRegularFile = typeFlag is '0' or '\0';
                bool isGoSourceFile = entryName.EndsWith(".go") || entryName.EndsWith("go.mod");
                bool shouldExtract = isRegularFile && isGoSourceFile;

                if (shouldExtract && entrySize > 0)
                {
                    var relativePath = entryName;
                    if (relativePath.StartsWith("go/"))
                    {
                        relativePath = relativePath.Substring(3);
                    }

                    var targetPath = Path.Combine(
                        outputDirectory,
                        relativePath.Replace('/', Path.DirectorySeparatorChar));

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                    var fileData = new byte[entrySize];
                    ReadFull(tarStream, fileData, 0, (int)entrySize);
                    File.WriteAllBytes(targetPath, fileData);

                    var paddingRemainder = (int)(entrySize % 512);
                    if (paddingRemainder > 0)
                    {
                        ReadFull(tarStream, new byte[512 - paddingRemainder], 0, 512 - paddingRemainder);
                    }
                }
                else if (entrySize > 0)
                {
                    long totalToSkip = entrySize;
                    var paddingRemainder = (int)(entrySize % 512);
                    if (paddingRemainder > 0)
                    {
                        totalToSkip += 512 - paddingRemainder;
                    }

                    var skipBuffer = new byte[4096];
                    while (totalToSkip > 0)
                    {
                        int bytesToRead = (int)Math.Min(totalToSkip, skipBuffer.Length);
                        int bytesActuallyRead = tarStream.Read(skipBuffer, 0, bytesToRead);
                        if (bytesActuallyRead == 0)
                        {
                            break;
                        }

                        totalToSkip -= bytesActuallyRead;
                    }
                }
            }
        }

        private static int ReadFull(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = stream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }

        private static string ReadTarString(byte[] buffer, int offset, int length)
        {
            int endPosition = offset;
            while (endPosition < offset + length && buffer[endPosition] != 0)
            {
                endPosition++;
            }

            return Encoding.ASCII.GetString(buffer, offset, endPosition - offset);
        }
    }
}
