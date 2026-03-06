// -----------------------------------------------------------------------
// <copyright file="GoOs.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoOs
    {
        // os.Args []string — returns command-line arguments
        public static Slice<string> Args
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                return new Slice<string>(args);
            }
        }

        public static void Exit(long code)
        {
            Environment.Exit((int)code);
        }

        public static string Getenv(string key)
        {
            return Environment.GetEnvironmentVariable(key) ?? "";
        }

        public static void Setenv(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        public static (string, bool) LookupEnv(string key)
        {
            var val = Environment.GetEnvironmentVariable(key);
            return val != null ? (val, true) : ("", false);
        }

        public static Slice<string> Environ()
        {
            var envVars = Environment.GetEnvironmentVariables();
            var result = new string[envVars.Count];
            int i = 0;
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                result[i++] = $"{entry.Key}={entry.Value}";
            }

            return new Slice<string>(result);
        }

        public static bool IsNotExist(object? err)
        {
            if (err is string s)
                return s.Contains("does not exist") || s.Contains("no such file")
                    || s.Contains("not found");
            return false;
        }

        public static bool IsExist(object? err)
        {
            if (err is string s)
                return s.Contains("already exists");
            return false;
        }

        public static bool IsPermission(object? err)
        {
            if (err is string s)
                return s.Contains("permission denied") || s.Contains("access denied");
            return false;
        }

        // os.Create(name string) (*File, error)
        public static (GoFile, object?) Create(string name)
        {
            try
            {
                var stream = File.Create(name);
                return (new GoFile(stream, name), null);
            }
            catch (Exception ex)
            {
                return (GoFile.Null, ex.Message);
            }
        }

        // os.Open(name string) (*File, error)
        public static (GoFile, object?) Open(string name)
        {
            try
            {
                var stream = File.OpenRead(name);
                return (new GoFile(stream, name), null);
            }
            catch (Exception ex)
            {
                return (GoFile.Null, ex.Message);
            }
        }

        // os.ReadFile(name string) ([]byte, error)
        public static (Slice<byte>, object?) ReadFile(string name)
        {
            try
            {
                var bytes = File.ReadAllBytes(name);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        // os.WriteFile(name string, data []byte, perm FileMode) error
        public static object? WriteFile(string name, Slice<byte> data, long perm)
        {
            try
            {
                var bytes = new byte[data.Len];
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] = data[i];
                File.WriteAllBytes(name, bytes);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Remove(name string) error
        public static object? Remove(string name)
        {
            try
            {
                File.Delete(name);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.MkdirAll(path string, perm FileMode) error
        public static object? MkdirAll(string path, long perm)
        {
            try
            {
                Directory.CreateDirectory(path);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Getwd() (string, error)
        public static (string, object?) Getwd()
        {
            try
            {
                return (Directory.GetCurrentDirectory(), null);
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // os.Rename(oldpath, newpath string) error
        public static object? Rename(string oldpath, string newpath)
        {
            try
            {
                File.Move(oldpath, newpath);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Stat(name string) (FileInfo, error)
        public static (GoFileInfo, object?) Stat(string name)
        {
            try
            {
                if (File.Exists(name))
                {
                    var info = new FileInfo(name);
                    return (new GoFileInfo(info.Name, info.Length, info.Attributes.HasFlag(FileAttributes.Directory)), null);
                }
                if (Directory.Exists(name))
                {
                    return (new GoFileInfo(Path.GetFileName(name), 0, true), null);
                }
                return (GoFileInfo.Empty, $"stat {name}: no such file or directory");
            }
            catch (Exception ex)
            {
                return (GoFileInfo.Empty, ex.Message);
            }
        }

        // os.TempDir() string
        public static string TempDir()
        {
            return Path.GetTempPath();
        }

        // os.UserHomeDir() (string, error)
        public static (string, object?) UserHomeDir()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                return ("", "os: user home directory not found");
            return (home, null);
        }

        // os.ReadDir(name string) ([]DirEntry, error)
        public static (Slice<GoDirEntry>, object?) ReadDir(string name)
        {
            try
            {
                var entries = new System.Collections.Generic.List<GoDirEntry>();
                foreach (var dir in Directory.GetDirectories(name))
                {
                    entries.Add(new GoDirEntry(Path.GetFileName(dir), true));
                }
                foreach (var file in Directory.GetFiles(name))
                {
                    entries.Add(new GoDirEntry(Path.GetFileName(file), false));
                }
                entries.Sort((a, b) => string.Compare(a.NameValue, b.NameValue, StringComparison.Ordinal));
                return (new Slice<GoDirEntry>(entries.ToArray()), null);
            }
            catch (Exception ex)
            {
                return (new Slice<GoDirEntry>(Array.Empty<GoDirEntry>()), ex.Message);
            }
        }

        // os.Chmod(name string, mode FileMode) error
        public static object? Chmod(string name, long mode)
        {
            try
            {
                // Simplified: .NET doesn't have full Unix chmod
                if ((mode & 0x80) == 0) // no owner write
                {
                    File.SetAttributes(name, File.GetAttributes(name) | FileAttributes.ReadOnly);
                }
                else
                {
                    var attrs = File.GetAttributes(name);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(name, attrs & ~FileAttributes.ReadOnly);
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Stdin, os.Stdout, os.Stderr
        public static readonly GoFile Stdin = new GoFile(Console.OpenStandardInput(), "/dev/stdin");
        public static readonly GoFile Stdout = new GoFile(Console.OpenStandardOutput(), "/dev/stdout");
        public static readonly GoFile Stderr = new GoFile(Console.OpenStandardError(), "/dev/stderr");
    }

    public sealed class GoFileInfo
    {
        public static readonly GoFileInfo Empty = new GoFileInfo("", 0, false);

        public string NameValue { get; }
        public long SizeValue { get; }
        public bool IsDirValue { get; }

        public GoFileInfo(string name, long size, bool isDir)
        {
            NameValue = name;
            SizeValue = size;
            IsDirValue = isDir;
        }

        public string Name() => NameValue;
        public long Size() => SizeValue;
        public bool IsDir() => IsDirValue;

        public override string ToString() => NameValue;
    }

    public sealed class GoDirEntry
    {
        public string NameValue { get; }
        public bool IsDirValue { get; }

        public GoDirEntry(string name, bool isDir)
        {
            NameValue = name;
            IsDirValue = isDir;
        }

        public string Name() => NameValue;
        public bool IsDir() => IsDirValue;

        public override string ToString() => NameValue;
    }

    /// <summary>
    /// Represents Go's *os.File, implementing IGoReader, IGoWriter, IGoCloser.
    /// </summary>
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

        public string Name() => _name;

        public (int, string) Read(Slice<byte> p)
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

        public (int, string) Write(Slice<byte> p)
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
        public (long, string) WriteString(string s)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                _stream.Write(bytes, 0, bytes.Length);
                return (bytes.Length, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

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

        public override string ToString() => $"&{{{_name}}}";
    }
}
