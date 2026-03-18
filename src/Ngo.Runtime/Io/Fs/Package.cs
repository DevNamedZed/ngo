using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Io.Fs
{
    /// <summary>
    /// Runtime support for Go's io/fs package.
    /// </summary>
    [GoPackage("io/fs")]
    public static class Package
    {
        // Sentinel errors
        [GoVar(Type = "error")]
        public static readonly object SkipDir = new Exception("skip this directory");

        [GoVar(Type = "error")]
        public static readonly object SkipAll = new Exception("skip everything and stop the walk");

        [GoVar(Type = "error")]
        public static readonly object ErrNotExist = new Exception("file does not exist");

        [GoVar(Type = "error")]
        public static readonly object ErrExist = new Exception("file already exists");

        [GoVar(Type = "error")]
        public static readonly object ErrPermission = new Exception("permission denied");

        [GoVar(Type = "error")]
        public static readonly object ErrClosed = new Exception("file already closed");

        [GoVar(Type = "error")]
        public static readonly object ErrInvalid = new Exception("invalid argument");

        // FileMode constants
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeDir = 0x80000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeAppend = 0x40000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeExclusive = 0x20000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeTemporary = 0x10000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeSymlink = 0x08000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeDevice = 0x04000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeNamedPipe = 0x02000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeSocket = 0x01000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeSetuid = 0x00800000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeSetgid = 0x00400000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeCharDevice = 0x00200000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeSticky = 0x00100000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeIrregular = 0x00080000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModeType = 0xFF000000;
        [GoConst(Type = "fs.FileMode")]
        public const uint ModePerm = 0x1FF;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? WalkDir([GoParam("fs.FS")] object? fsys, string root,
            [GoParam("func(string, fs.DirEntry, error) error")] Func<string, object?, object?, object?> fn)
        {
            try
            {
                return WalkDirImpl(root, fn);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static object? WalkDirImpl(string path, Func<string, object?, object?, object?> fn)
        {
            if (System.IO.File.Exists(path))
            {
                var entry = new FsDirEntry(System.IO.Path.GetFileName(path), false);
                return fn(path, entry, null);
            }

            if (!System.IO.Directory.Exists(path))
            {
                var errMsg = $"open {path}: no such file or directory";
                return fn(path, null, errMsg);
            }

            var dirEntry = new FsDirEntry(System.IO.Path.GetFileName(path), true);
            var err = fn(path, dirEntry, null);
            if (err != null)
            {
                if (ReferenceEquals(err, SkipDir))
                {
                    return null;
                }
                if (ReferenceEquals(err, SkipAll))
                {
                    return null;
                }
                return err;
            }

            var entries = System.IO.Directory.GetFileSystemEntries(path);
            Array.Sort(entries, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                err = WalkDirImpl(entry, fn);
                if (err != null)
                {
                    if (ReferenceEquals(err, SkipAll))
                    {
                        return null;
                    }
                    return err;
                }
            }
            return null;
        }

        [GoFunc]
        [return: GoReturn("fs.FS", "error")]
        public static (object?, object?) Sub([GoParam("fs.FS")] object? fsys, string dir)
        {
            if (fsys is IGoSubFS subFS)
            {
                return subFS.Sub(dir);
            }
            return (new SubDirFS(dir), null);
        }

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) ReadFile([GoParam("fs.FS")] object? fsys, string name)
        {
            // Try the FS interface first
            if (fsys is IGoReadFileFS readFileFS)
            {
                return readFileFS.ReadFile(name);
            }
            // Fall back to system file
            try
            {
                var bytes = System.IO.File.ReadAllBytes(name);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoFunc]
        public static bool ValidPath(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name == ".") return true;
            if (name.StartsWith("/") || name.StartsWith("\\")) return false;
            if (name.Contains("..")) return false;
            return true;
        }

        [GoFunc]
        public static string FormatFileInfo(object info)
        {
            return info?.ToString() ?? "<nil>";
        }

        [GoFunc]
        public static string FormatDirEntry(object dir)
        {
            return dir?.ToString() ?? "<nil>";
        }

        [GoFunc]
        public static object FileInfoToDirEntry(object info)
        {
            return new FsDirEntry("", false);
        }

        [GoFunc]
        public static (object, string) Stat(object fsys, string name)
        {
            try
            {
                if (System.IO.File.Exists(name))
                {
                    var info = new System.IO.FileInfo(name);
                    return (new FsFileInfo(info.Name, info.Length, false), null!);
                }
                if (System.IO.Directory.Exists(name))
                {
                    return (new FsFileInfo(System.IO.Path.GetFileName(name), 0, true), null!);
                }
                return (null!, $"stat {name}: no such file or directory");
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoFunc]
        public static (Slice<object>, string) ReadDir(object fsys, string name)
        {
            try
            {
                var entries = System.IO.Directory.GetFileSystemEntries(name);
                Array.Sort(entries, StringComparer.Ordinal);
                var result = new object[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    bool isDir = System.IO.Directory.Exists(entries[i]);
                    result[i] = new FsDirEntry(System.IO.Path.GetFileName(entries[i]), isDir);
                }
                return (new Slice<object>(result), null!);
            }
            catch (Exception ex)
            {
                return (new Slice<object>(Array.Empty<object>()), ex.Message);
            }
        }

        [GoFunc]
        public static (Slice<string>, string) Glob(object fsys, string pattern)
        {
            return (new Slice<string>(Array.Empty<string>()), null!);
        }
    }

    [GoType("interface", Name = "File", Package = "io/fs")]
    public interface IGoFile
    {
        [return: GoReturn("fs.FileInfo", "error")]
        (IGoFileInfo, object?) Stat();

        [return: GoReturn("int", "error")]
        (long, object?) Read(Slice<byte> p);

        [return: GoReturn("error")]
        object? Close();
    }

    [GoType("interface", Name = "FS", Package = "io/fs")]
    public interface IGoFS
    {
        [return: GoReturn("fs.File", "error")]
        (IGoFile, string) Open(string name);
    }

    [GoType("named", Name = "FileMode", Underlying = "uint32", Package = "io/fs")]
    public struct GoFileMode
    {
        public uint Value;

        public GoFileMode(uint v) { Value = v; }

        [GoMethod]
        public bool IsDir() => (Value & Package.ModeDir) != 0;

        [GoMethod]
        public bool IsRegular() => (Value & Package.ModeType) == 0;

        [GoMethod]
        public GoFileMode Perm() => new GoFileMode(Value & Package.ModePerm);

        [GoMethod]
        public string String() => Value.ToString("o");

        [GoMethod]
        public GoFileMode Type() => new GoFileMode(Value & Package.ModeType);
    }

    [GoType("interface", Name = "FileInfo", Package = "io/fs")]
    public interface IGoFileInfo
    {
        string Name();
        long Size();
        GoFileMode Mode();
        object ModTime();
        bool IsDir();
        object Sys();
    }

    [GoType("interface", Name = "DirEntry", Package = "io/fs")]
    public interface IGoDirEntry
    {
        string Name();
        bool IsDir();
        GoFileMode Type();
        [return: GoReturn("fs.FileInfo", "error")]
        (IGoFileInfo, string) Info();
    }

    [GoType("interface", Name = "ReadDirFile", Package = "io/fs")]
    public interface IGoReadDirFile : IGoFile
    {
        [return: GoReturn("[]fs.DirEntry", "error")]
        (Slice<IGoDirEntry>, string) ReadDir(long n);
    }

    // WalkDirFunc is the type of the function called by WalkDir to visit
    // each file or directory.
    [GoType("named", Name = "WalkDirFunc", Package = "io/fs", Underlying = "func(string, fs.DirEntry, error) error")]
    public class GoWalkDirFunc
    {
    }

    // ReadDirFS is the interface implemented by a file system
    // that provides an optimized implementation of ReadDir.
    [GoType("interface", Name = "ReadDirFS", Package = "io/fs")]
    public interface IGoReadDirFS
    {
        [return: GoReturn("[]fs.DirEntry", "error")]
        (Slice<object>, object?) ReadDir(string name);
    }

    // GlobFS is the interface implemented by a file system
    // that provides an optimized implementation of Glob.
    [GoType("interface", Name = "GlobFS", Package = "io/fs")]
    public interface IGoGlobFS
    {
        [return: GoReturn("[]string", "error")]
        (Slice<string>, object?) Glob(string pattern);
    }

    // StatFS is the interface implemented by a file system
    // that provides an optimized implementation of Stat.
    [GoType("interface", Name = "StatFS", Package = "io/fs")]
    public interface IGoStatFS
    {
        [return: GoReturn("fs.FileInfo", "error")]
        (object, object?) Stat(string name);
    }

    // ReadFileFS is the interface implemented by a file system
    // that provides an optimized implementation of ReadFile.
    [GoType("interface", Name = "ReadFileFS", Package = "io/fs")]
    public interface IGoReadFileFS
    {
        [return: GoReturn("[]byte", "error")]
        (Slice<byte>, object?) ReadFile(string name);
    }

    internal class FsFileInfo : IGoFileInfo
    {
        private readonly string _name;
        private readonly long _size;
        private readonly bool _isDir;

        public FsFileInfo(string name, long size, bool isDir)
        {
            _name = name;
            _size = size;
            _isDir = isDir;
        }

        public string Name() => _name;
        public long Size() => _size;
        public GoFileMode Mode() => _isDir ? new GoFileMode(Package.ModeDir | Package.ModePerm) : new GoFileMode(Package.ModePerm);
        public object ModTime() => new object();
        public bool IsDir() => _isDir;
        public object Sys() => null!;
    }

    internal class SubDirFS : IGoFS
    {
        private readonly string _dir;

        public SubDirFS(string dir)
        {
            _dir = dir;
        }

        public (IGoFile, string) Open(string name)
        {
            var fullPath = System.IO.Path.Combine(_dir, name);
            if (!System.IO.File.Exists(fullPath))
            {
                return (null!, $"open {name}: no such file or directory");
            }
            return (null!, null!);
        }
    }

    internal class FsDirEntry : IGoDirEntry
    {
        private readonly string _name;
        private readonly bool _isDir;

        public FsDirEntry(string name, bool isDir)
        {
            _name = name;
            _isDir = isDir;
        }

        public string Name() => _name;
        public bool IsDir() => _isDir;
        public GoFileMode Type() => _isDir ? new GoFileMode(Package.ModeDir) : new GoFileMode(0);
        public (IGoFileInfo, string) Info() => (null!, null!);
    }

    // SubFS is the interface implemented by a file system
    // that provides an optimized implementation of Sub.
    [GoType("interface", Name = "SubFS", Package = "io/fs")]
    public interface IGoSubFS
    {
        [return: GoReturn("fs.FS", "error")]
        (object, object?) Sub(string dir);
    }

    [GoType("struct", Name = "PathError", Package = "io/fs")]
    public class GoPathError
    {
        [GoField(Name = "Op")] public string Op;
        [GoField(Name = "Path")] public string Path;
        [GoField(Name = "Err", Type = "error")] public object Err;

        [GoMethod]
        public string Error() => $"{Op} {Path}: {Err}";
        [GoMethod]
        [return: GoReturn("error")]
        public object Unwrap() => Err;
    }
}
