// -----------------------------------------------------------------------
// <copyright file="GoFileInfo.cs" company="Ziad">
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

namespace Ngo.Runtime.Os
{
    [GoType("interface", Name = "FileInfo", Package = "os")]
    public sealed class GoFileInfo
    {
        public static readonly GoFileInfo Empty = new GoFileInfo("", 0, false);

        public string NameValue { get; }
        public long SizeValue { get; }
        public bool IsDirValue { get; }

        private readonly System.DateTimeOffset _modTime;

        public GoFileInfo(string name, long size, bool isDir)
            : this(name, size, isDir, System.DateTimeOffset.MinValue) { }

        public GoFileInfo(string name, long size, bool isDir, System.DateTimeOffset modTime)
        {
            NameValue = name;
            SizeValue = size;
            IsDirValue = isDir;
            _modTime = modTime;
        }

        [GoMethod]
        public string Name() => NameValue;
        [GoMethod]
        [return: GoReturn("int64")]
        public long Size() => SizeValue;
        [GoMethod]
        public bool IsDir() => IsDirValue;

        [GoMethod]
        [return: GoReturn("FileMode")]
        public long Mode()
        {
            long mode = 0x1FF; // 0777 default
            if (IsDirValue) mode |= unchecked((long)0x80000000); // ModeDir
            return mode;
        }

        [GoMethod]
        public object ModTime() => new Time.GoTimeValue(_modTime);

        [GoMethod]
        public object? Sys() => null;

        public override string ToString() => NameValue;

        internal static GoFileInfo FromPath(string path)
        {
            bool isDir = System.IO.Directory.Exists(path);
            long size = 0;
            System.DateTimeOffset modTime = System.DateTimeOffset.MinValue;
            if (!isDir && System.IO.File.Exists(path))
            {
                try
                {
                    var info = new System.IO.FileInfo(path);
                    size = info.Length;
                    modTime = new System.DateTimeOffset(info.LastWriteTimeUtc, System.TimeSpan.Zero);
                }
                catch
                {
                    // Ignore errors reading metadata
                }
            }
            else if (isDir)
            {
                try
                {
                    var info = new System.IO.DirectoryInfo(path);
                    modTime = new System.DateTimeOffset(info.LastWriteTimeUtc, System.TimeSpan.Zero);
                }
                catch
                {
                    // Ignore errors reading metadata
                }
            }
            return new GoFileInfo(System.IO.Path.GetFileName(path), size, isDir, modTime);
        }
    }
}
