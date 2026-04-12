// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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

namespace Ngo.Runtime.Os
{
    [GoPackage("os")]
    public static class GoOs
    {
        // ---- Constants ----

        [GoConst]
        public const long O_RDONLY = 0;
        [GoConst]
        public const long O_WRONLY = 1;
        [GoConst]
        public const long O_RDWR = 2;
        [GoConst]
        public const long O_APPEND = 0x400;
        [GoConst]
        public const long O_CREATE = 0x40;
        [GoConst]
        public const long O_EXCL = 0x80;
        [GoConst]
        public const long O_SYNC = 0x101000;
        [GoConst]
        public const long O_TRUNC = 0x200;

        [GoConst(Type = "rune")]
        public const long PathSeparator = '/';
        [GoConst(Type = "rune")]
        public const long PathListSeparator = ':';

        [GoConst]
        public const string DevNull = "/dev/null";

        [GoConst]
        public const long SEEK_SET = 0;
        [GoConst]
        public const long SEEK_CUR = 1;
        [GoConst]
        public const long SEEK_END = 2;

        // FileMode constants
        [GoConst(Type = "FileMode")]
        public const long ModeDir = unchecked((long)0x80000000);
        [GoConst(Type = "FileMode")]
        public const long ModeAppend = 0x40000000;
        [GoConst(Type = "FileMode")]
        public const long ModeExclusive = 0x20000000;
        [GoConst(Type = "FileMode")]
        public const long ModeTemporary = 0x10000000;
        [GoConst(Type = "FileMode")]
        public const long ModeSymlink = 0x08000000;
        [GoConst(Type = "FileMode")]
        public const long ModeDevice = 0x04000000;
        [GoConst(Type = "FileMode")]
        public const long ModeNamedPipe = 0x02000000;
        [GoConst(Type = "FileMode")]
        public const long ModeSocket = 0x01000000;
        [GoConst(Type = "FileMode")]
        public const long ModeSetuid = 0x00800000;
        [GoConst(Type = "FileMode")]
        public const long ModeSetgid = 0x00400000;
        [GoConst(Type = "FileMode")]
        public const long ModeCharDevice = 0x00200000;
        [GoConst(Type = "FileMode")]
        public const long ModeSticky = 0x00100000;
        [GoConst(Type = "FileMode")]
        public const long ModeIrregular = 0x00080000;
        [GoConst(Type = "FileMode")]
        public const long ModeType = unchecked((long)0xFF000000);
        [GoConst(Type = "FileMode")]
        public const long ModePerm = 0x1FF;

        // ---- Package variables ----

        /// <summary>Set by ngo CLI to provide filtered args to the Go program.</summary>
        public static string[]? OverrideArgs { get; set; }

        // os.Args []string
        [GoVar]
        public static Slice<string> Args
        {
            get
            {
                var args = OverrideArgs ?? Environment.GetCommandLineArgs();
                return new Slice<string>(args);
            }
        }

        // os.Stdin, os.Stdout, os.Stderr
        [GoVar(Type = "*File")]
        public static readonly GoFile Stdin = new GoFile(Console.OpenStandardInput(), "/dev/stdin");
        [GoVar(Type = "*File")]
        public static readonly GoFile Stdout = new GoFile(Console.OpenStandardOutput(), "/dev/stdout");
        [GoVar(Type = "*File")]
        public static readonly GoFile Stderr = new GoFile(Console.OpenStandardError(), "/dev/stderr");

        // Error sentinel variables
        [GoVar(Type = "error")]
        public static readonly object ErrNotExist = Ngo.Runtime.Errors.Package.New("file does not exist");
        [GoVar(Type = "error")]
        public static readonly object ErrExist = Ngo.Runtime.Errors.Package.New("file already exists");
        [GoVar(Type = "error")]
        public static readonly object ErrPermission = Ngo.Runtime.Errors.Package.New("permission denied");
        [GoVar(Type = "error")]
        public static readonly object ErrClosed = Ngo.Runtime.Errors.Package.New("file already closed");
        [GoVar(Type = "error")]
        public static readonly object ErrDeadlineExceeded = Ngo.Runtime.Errors.Package.New("i/o timeout");
        [GoVar(Type = "error")]
        public static readonly object ErrInvalid = Ngo.Runtime.Errors.Package.New("invalid argument");
        [GoVar(Type = "error")]
        public static readonly object ErrProcessDone = Ngo.Runtime.Errors.Package.New("os: process already finished");

        // Signal variables
        [GoVar(Type = "Signal")]
        public static readonly object Interrupt = new GoOsSignal(2, "interrupt");
        [GoVar(Type = "Signal")]
        public static readonly object Kill = new GoOsSignal(9, "killed");

        // ---- Functions ----

        [GoFunc]
        public static void Exit(long code)
        {
            Environment.Exit((int)code);
        }

        [GoFunc]
        public static GoString Getenv(GoString key)
        {
            return GoString.FromNetString(Environment.GetEnvironmentVariable(key.ToNetString()) ?? "");
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Setenv(GoString key, GoString value)
        {
            Environment.SetEnvironmentVariable(key.ToNetString(), value.ToNetString());
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unsetenv(GoString key)
        {
            Environment.SetEnvironmentVariable(key.ToNetString(), null);
            return null;
        }

        [GoFunc]
        public static void Clearenv()
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                Environment.SetEnvironmentVariable((string)entry.Key, null);
            }
        }

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (GoString, bool) LookupEnv(GoString key)
        {
            var val = Environment.GetEnvironmentVariable(key.ToNetString());
            return val != null ? (GoString.FromNetString(val), true) : (default, false);
        }

        [GoFunc]
        public static Slice<GoString> Environ()
        {
            var envVars = Environment.GetEnvironmentVariables();
            var result = new GoString[envVars.Count];
            int i = 0;
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                result[i++] = GoString.FromNetString($"{entry.Key}={entry.Value}");
            }

            return new Slice<GoString>(result);
        }

        [GoFunc]
        public static bool IsNotExist([GoParam("error")] object? err)
        {
            if (err is string s)
                return s.Contains("does not exist") || s.Contains("no such file")
                    || s.Contains("not found");
            return false;
        }

        [GoFunc]
        public static bool IsExist([GoParam("error")] object? err)
        {
            if (err is string s)
                return s.Contains("already exists");
            return false;
        }

        [GoFunc]
        public static bool IsPermission([GoParam("error")] object? err)
        {
            if (err is string s)
                return s.Contains("permission denied") || s.Contains("access denied");
            return false;
        }

        [GoFunc]
        public static bool IsTimeout([GoParam("error")] object? err)
        {
            if (err is string s)
                return s.Contains("timeout") || s.Contains("timed out");
            return false;
        }

        // os.Create(name string) (*File, error)
        [GoFunc]
        [return: GoReturn("*File", "error")]
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
        [GoFunc]
        [return: GoReturn("*File", "error")]
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

        // os.OpenFile(name string, flag int, perm FileMode) (*File, error)
        [GoFunc]
        [return: GoReturn("*File", "error")]
        public static (GoFile, object?) OpenFile(string name, long flag, [GoParam("FileMode")] long perm)
        {
            try
            {
                FileMode fileMode = FileMode.Open;
                FileAccess access = FileAccess.Read;

                if ((flag & O_CREATE) != 0)
                    fileMode = FileMode.OpenOrCreate;
                if ((flag & O_TRUNC) != 0)
                    fileMode = (flag & O_CREATE) != 0 ? FileMode.Create : FileMode.Truncate;
                if ((flag & O_EXCL) != 0 && (flag & O_CREATE) != 0)
                    fileMode = FileMode.CreateNew;
                if ((flag & O_APPEND) != 0)
                    fileMode = FileMode.Append;

                if ((flag & O_RDWR) != 0)
                    access = FileAccess.ReadWrite;
                else if ((flag & O_WRONLY) != 0)
                    access = FileAccess.Write;

                var stream = new FileStream(name, fileMode, access);
                return (new GoFile(stream, name), null);
            }
            catch (Exception ex)
            {
                return (GoFile.Null, ex.Message);
            }
        }

        // os.ReadFile(name string) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
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
        [GoFunc]
        [return: GoReturn("error")]
        public static object? WriteFile(string name, Slice<byte> data, [GoParam("FileMode")] long perm)
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
        [GoFunc]
        [return: GoReturn("error")]
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

        // os.RemoveAll(path string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? RemoveAll(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Mkdir(path string, perm FileMode) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Mkdir(string path, [GoParam("FileMode")] long perm)
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

        // os.MkdirAll(path string, perm FileMode) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? MkdirAll(string path, [GoParam("FileMode")] long perm)
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

        // os.MkdirTemp(dir, pattern string) (string, error)
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) MkdirTemp(string dir, string pattern)
        {
            try
            {
                if (string.IsNullOrEmpty(dir))
                    dir = global::System.IO.Path.GetTempPath();
                string name = global::System.IO.Path.Combine(dir, pattern + global::System.IO.Path.GetRandomFileName());
                Directory.CreateDirectory(name);
                return (name, null);
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // os.CreateTemp(dir, pattern string) (*File, error)
        [GoFunc]
        [return: GoReturn("*File", "error")]
        public static (GoFile, object?) CreateTemp(string dir, string pattern)
        {
            try
            {
                if (string.IsNullOrEmpty(dir))
                    dir = global::System.IO.Path.GetTempPath();
                string name = global::System.IO.Path.Combine(dir, pattern + global::System.IO.Path.GetRandomFileName());
                var stream = File.Create(name);
                return (new GoFile(stream, name), null);
            }
            catch (Exception ex)
            {
                return (GoFile.Null, ex.Message);
            }
        }

        // os.Getwd() (string, error)
        [GoFunc]
        [return: GoReturn("string", "error")]
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
        [GoFunc]
        [return: GoReturn("error")]
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
        [GoFunc]
        [return: GoReturn("FileInfo", "error")]
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
                    return (new GoFileInfo(global::System.IO.Path.GetFileName(name), 0, true), null);
                }
                return (GoFileInfo.Empty, $"stat {name}: no such file or directory");
            }
            catch (Exception ex)
            {
                return (GoFileInfo.Empty, ex.Message);
            }
        }

        // os.Lstat(name string) (FileInfo, error)
        [GoFunc]
        [return: GoReturn("FileInfo", "error")]
        public static (GoFileInfo, object?) Lstat(string name)
        {
            // Simplified: same as Stat (no symlink distinction on .NET)
            return Stat(name);
        }

        // os.TempDir() string
        [GoFunc]
        public static string TempDir()
        {
            return global::System.IO.Path.GetTempPath();
        }

        // os.UserHomeDir() (string, error)
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) UserHomeDir()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                return ("", "os: user home directory not found");
            return (home, null);
        }

        // os.ReadDir(name string) ([]DirEntry, error)
        [GoFunc]
        [return: GoReturn("[]DirEntry", "error")]
        public static (Slice<GoDirEntry>, object?) ReadDir(string name)
        {
            try
            {
                var entries = new System.Collections.Generic.List<GoDirEntry>();
                foreach (var dir in Directory.GetDirectories(name))
                {
                    entries.Add(new GoDirEntry(global::System.IO.Path.GetFileName(dir), true));
                }
                foreach (var file in Directory.GetFiles(name))
                {
                    entries.Add(new GoDirEntry(global::System.IO.Path.GetFileName(file), false));
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
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chmod(string name, [GoParam("FileMode")] long mode)
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

        // os.Chown(name string, uid, gid int) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chown(string name, long uid, long gid)
        {
            // No-op on .NET / Windows
            return null;
        }

        // os.Chtimes(name string, atime time.Time, mtime time.Time) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chtimes(string name, [GoParam("interface{}")] object atime, [GoParam("interface{}")] object mtime)
        {
            try
            {
                if (atime is Time.GoTimeValue atv)
                {
                    File.SetLastAccessTimeUtc(name, atv.Value.UtcDateTime);
                }
                if (mtime is Time.GoTimeValue mtv)
                {
                    File.SetLastWriteTimeUtc(name, mtv.Value.UtcDateTime);
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Link(oldname, newname string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Link(string oldname, string newname)
        {
            try
            {
                File.Copy(oldname, newname);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Symlink(oldname, newname string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Symlink(string oldname, string newname)
        {
            try
            {
                File.CreateSymbolicLink(newname, oldname);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Readlink(name string) (string, error)
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) Readlink(string name)
        {
            try
            {
                var target = File.ResolveLinkTarget(name, false);
                if (target != null)
                    return (target.FullName, null);
                return ("", $"readlink {name}: not a symbolic link");
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // os.Hostname() (string, error)
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) Hostname()
        {
            try
            {
                return (Environment.MachineName, null);
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // os.Executable() (string, error)
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) Executable()
        {
            try
            {
                var path = Environment.ProcessPath;
                if (path != null)
                    return (path, null);
                return ("", "os: could not determine executable path");
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // os.Getpagesize() int
        [GoFunc]
        public static long Getpagesize()
        {
            return Environment.SystemPageSize;
        }

        // os.Getuid() int
        [GoFunc]
        public static long Getuid()
        {
            return -1; // Not available on .NET/Windows
        }

        // os.Getgid() int
        [GoFunc]
        public static long Getgid()
        {
            return -1;
        }

        // os.Getpid() int
        [GoFunc]
        public static long Getpid()
        {
            return Environment.ProcessId;
        }

        // os.FindProcess(pid int) (*Process, error)
        [GoFunc]
        [return: GoReturn("*Process", "error")]
        public static (GoProcess, object?) FindProcess(long pid)
        {
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return (new GoProcess(proc), null);
            }
            catch (Exception ex)
            {
                return (GoProcess.Null, ex.Message);
            }
        }

        // os.StartProcess(name string, argv []string, attr *ProcAttr) (*Process, error)
        [GoFunc]
        [return: GoReturn("*Process", "error")]
        public static (GoProcess, object?) StartProcess(string name, Slice<string> argv, [GoParam("*ProcAttr")] object? attr)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(name);
                for (int i = 0; i < argv.Len; i++)
                    psi.ArgumentList.Add(argv[i]);
                var proc = System.Diagnostics.Process.Start(psi);
                return (proc != null ? new GoProcess(proc) : GoProcess.Null, null);
            }
            catch (Exception ex)
            {
                return (GoProcess.Null, ex.Message);
            }
        }

        // os.Pipe() (r *File, w *File, err error)
        [GoFunc]
        [return: GoReturn("*File", "*File", "error")]
        public static (GoFile, GoFile, object?) Pipe()
        {
            // Uses .NET AnonymousPipeStream for pipe pair
            var pipeIn = new System.IO.Pipes.AnonymousPipeServerStream(System.IO.Pipes.PipeDirection.Out);
            var pipeOut = new System.IO.Pipes.AnonymousPipeClientStream(System.IO.Pipes.PipeDirection.In, pipeIn.ClientSafePipeHandle);
            return (new GoFile(pipeOut, "|0"), new GoFile(pipeIn, "|1"), null);
        }

        [GoFunc]
        public static GoString Expand(GoString s, Func<GoString, GoString> mapping)
        {
            var str = s.ToNetString();
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < str.Length)
            {
                if (str[i] == '$' && i + 1 < str.Length)
                {
                    i++;
                    string varName;
                    if (str[i] == '{')
                    {
                        int end = str.IndexOf('}', i + 1);
                        if (end < 0)
                        {
                            sb.Append("${");
                            i++;
                            continue;
                        }
                        varName = str.Substring(i + 1, end - i - 1);
                        i = end + 1;
                    }
                    else
                    {
                        int start = i;
                        while (i < str.Length && (char.IsLetterOrDigit(str[i]) || str[i] == '_'))
                        {
                            i++;
                        }
                        varName = str.Substring(start, i - start);
                    }
                    sb.Append(mapping(GoString.FromNetString(varName)).ToNetString());
                }
                else
                {
                    sb.Append(str[i]);
                    i++;
                }
            }
            return GoString.FromNetString(sb.ToString());
        }

        [GoFunc]
        public static GoString ExpandEnv(GoString s)
        {
            return Expand(s, Getenv);
        }

        // os.SameFile(fi1, fi2 FileInfo) bool
        [GoFunc]
        public static bool SameFile([GoParam("FileInfo")] GoFileInfo fi1, [GoParam("FileInfo")] GoFileInfo fi2)
        {
            return fi1.Name() == fi2.Name() && fi1.Size() == fi2.Size() && fi1.IsDir() == fi2.IsDir();
        }

        // os.IsPathSeparator(c uint8) bool
        [GoFunc]
        public static bool IsPathSeparator([GoParam("uint8")] byte c)
        {
            return c == '/' || c == global::System.IO.Path.DirectorySeparatorChar;
        }

        // os.NewSyscallError(syscall string, err error) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? NewSyscallError(string syscall, [GoParam("error")] object? err)
        {
            if (err == null) return null;
            return new GoSyscallError(syscall, err);
        }

        // os.Chdir(dir string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chdir(string dir)
        {
            try
            {
                Directory.SetCurrentDirectory(dir);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.NewFile(fd uintptr, name string) *File
        [GoFunc]
        [return: GoReturn("*File")]
        public static GoFile? NewFile(long fd, string name)
        {
            return null;
        }

        // os.Getppid() int
        [GoFunc]
        public static long Getppid()
        {
            return 0;
        }

        // os.Geteuid() int
        [GoFunc]
        public static long Geteuid()
        {
            return 0;
        }

        // os.DirFS(dir string) fs.FS
        [GoFunc]
        [return: GoReturn("interface{}")]
        public static object DirFS(string dir)
        {
            return dir;
        }
    }
}
