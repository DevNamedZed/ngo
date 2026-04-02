// -----------------------------------------------------------------------
// <copyright file="GoFile.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Os
{
    /// <summary>
    /// Represents Go's *os.File, implementing IGoReader, IGoWriter, IGoCloser.
    /// </summary>
    [GoType("struct", Name = "File", Package = "os")]
    public sealed class GoFile : IGoReader, IGoWriter, IGoCloser
    {
        private readonly Stream? _stream;
        private readonly string _name;

        public static readonly GoFile Null = new GoFile(null, "<nil>");

        public GoFile(Stream? stream, string name)
        {
            _stream = stream;
            _name = name;
        }

        [GoMethod]
        public string Name() => _name;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) Read(Slice<byte> p)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var buf = new byte[p.Len];
                int n = _stream.Read(buf, 0, buf.Length);
                for (int i = 0; i < n; i++)
                    p[i] = buf[i];
                if (n == 0) return (0, GoIo.EOF);
                return (n, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) Write(Slice<byte> p)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var buf = new byte[p.Len];
                for (int i = 0; i < buf.Length; i++)
                    buf[i] = p[i];
                _stream.Write(buf, 0, buf.Length);
                return (buf.Length, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        // WriteString writes a string directly
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) WriteString(string s)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
                _stream.Write(bytes, 0, bytes.Length);
                return (bytes.Length, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Close()
        {
            if (_stream == null) return "";
            try
            {
                _stream.Close();
                return "";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadAt(Slice<byte> b, [GoParam("int64")] long off)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                _stream.Seek(off, SeekOrigin.Begin);
                var buf = new byte[b.Len];
                int n = _stream.Read(buf, 0, buf.Length);
                for (int i = 0; i < n; i++)
                    b[i] = buf[i];
                if (n == 0) return (0, GoIo.EOF);
                return (n, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteAt(Slice<byte> b, [GoParam("int64")] long off)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                _stream.Seek(off, SeekOrigin.Begin);
                var buf = new byte[b.Len];
                for (int i = 0; i < buf.Length; i++)
                    buf[i] = b[i];
                _stream.Write(buf, 0, buf.Length);
                return (buf.Length, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Seek([GoParam("int64")] long offset, long whence)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                SeekOrigin origin = whence switch
                {
                    0 => SeekOrigin.Begin,
                    1 => SeekOrigin.Current,
                    2 => SeekOrigin.End,
                    _ => SeekOrigin.Begin,
                };
                long pos = _stream.Seek(offset, origin);
                return (pos, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Sync()
        {
            if (_stream == null) return null;
            try
            {
                _stream.Flush();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Truncate([GoParam("int64")] long size)
        {
            if (_stream == null) return "os: file is nil";
            try
            {
                _stream.SetLength(size);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("uintptr")]
        public long Fd()
        {
            if (_stream is FileStream fs)
            {
                return fs.SafeFileHandle.DangerousGetHandle().ToInt64();
            }
            return unchecked((long)0xFFFFFFFFFFFFFFFF); // -1 for invalid fd
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Chmod([GoParam("FileMode")] long mode)
        {
            return GoOs.Chmod(_name, mode);
        }

        [GoMethod]
        [return: GoReturn("FileInfo", "error")]
        public (GoFileInfo, object?) Stat()
        {
            return GoOs.Stat(_name);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Chown(long uid, long gid)
        {
            return null; // No-op on .NET
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetDeadline([GoParam("interface{}")] object t)
        {
            return null; // No-op
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadDeadline([GoParam("interface{}")] object t)
        {
            return null; // No-op
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteDeadline([GoParam("interface{}")] object t)
        {
            return null; // No-op
        }

        [GoMethod]
        [return: GoReturn("[]FileInfo", "error")]
        public (Slice<GoFileInfo>, object?) Readdir(long n)
        {
            try
            {
                if (!Directory.Exists(_name))
                {
                    return (new Slice<GoFileInfo>(Array.Empty<GoFileInfo>()), $"readdir {_name}: not a directory");
                }
                var entries = Directory.GetFileSystemEntries(_name);
                int count = n <= 0 ? entries.Length : (int)global::System.Math.Min(n, entries.Length);
                var infos = new GoFileInfo[count];
                for (int i = 0; i < count; i++)
                {
                    infos[i] = GoFileInfo.FromPath(entries[i]);
                }
                return (new Slice<GoFileInfo>(infos), null);
            }
            catch (Exception ex)
            {
                return (new Slice<GoFileInfo>(Array.Empty<GoFileInfo>()), ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("[]DirEntry", "error")]
        public (Slice<GoDirEntry>, object?) ReadDir(long n)
        {
            try
            {
                if (!Directory.Exists(_name))
                {
                    return (new Slice<GoDirEntry>(Array.Empty<GoDirEntry>()), $"readdir {_name}: not a directory");
                }
                var entries = Directory.GetFileSystemEntries(_name);
                Array.Sort(entries, StringComparer.Ordinal);
                int count = n <= 0 ? entries.Length : (int)global::System.Math.Min(n, entries.Length);
                var dirEntries = new GoDirEntry[count];
                for (int i = 0; i < count; i++)
                {
                    string entryName = global::System.IO.Path.GetFileName(entries[i]);
                    bool isDir = Directory.Exists(entries[i]);
                    dirEntries[i] = new GoDirEntry(entryName, isDir);
                }
                return (new Slice<GoDirEntry>(dirEntries), null);
            }
            catch (Exception ex)
            {
                return (new Slice<GoDirEntry>(Array.Empty<GoDirEntry>()), ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) Readdirnames(long n)
        {
            try
            {
                if (!Directory.Exists(_name))
                {
                    return (new Slice<string>(Array.Empty<string>()), $"readdir {_name}: not a directory");
                }
                var entries = Directory.GetFileSystemEntries(_name);
                int count = n <= 0 ? entries.Length : (int)global::System.Math.Min(n, entries.Length);
                var names = new string[count];
                for (int i = 0; i < count; i++)
                {
                    names[i] = global::System.IO.Path.GetFileName(entries[i]);
                }
                return (new Slice<string>(names), null);
            }
            catch (Exception ex)
            {
                return (new Slice<string>(Array.Empty<string>()), ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) ReadFrom([GoParam("io.Reader")] object r)
        {
            if (_stream == null)
            {
                return (0, "os: file not open");
            }
            if (r is IGoReader reader)
            {
                long total = 0;
                var buf = new byte[32768];
                while (true)
                {
                    var slice = new Slice<byte>(buf);
                    var (n, err) = reader.Read(slice);
                    if (n > 0)
                    {
                        _stream.Write(buf, 0, (int)n);
                        total += n;
                    }
                    if (err != null)
                    {
                        break;
                    }
                }
                return (total, null);
            }
            return (0, "os: invalid reader");
        }

        [GoMethod]
        [return: GoReturn("interface{}", "error")]
        public (object?, object?) SyscallConn()
        {
            return (null, "not implemented"); // Stub
        }

        public override string ToString() => $"&{{{_name}}}";
    }
}
