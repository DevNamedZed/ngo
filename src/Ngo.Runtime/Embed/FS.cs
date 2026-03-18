using System;
using System.Collections.Generic;
using System.IO;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Embed
{
    [GoType("struct", Name = "FS", Package = "embed")]
    public class FS
    {
        private string _basePath = "";

        internal void SetBasePath(string path)
        {
            _basePath = path;
        }

        private string ResolvePath(string name)
        {
            if (!string.IsNullOrEmpty(_basePath))
            {
                return System.IO.Path.Combine(_basePath, name);
            }
            return name;
        }

        [GoMethod]
        public (Slice<byte>, object?) ReadFile(string name)
        {
            try
            {
                var path = ResolvePath(name);
                var bytes = File.ReadAllBytes(path);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoMethod]
        public (Slice<DirEntry>, object?) ReadDir(string name)
        {
            try
            {
                var path = ResolvePath(name);
                if (!Directory.Exists(path))
                {
                    return (new Slice<DirEntry>(Array.Empty<DirEntry>()), $"open {name}: no such file or directory");
                }

                var entries = new List<DirEntry>();
                foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                {
                    var info = new FileInfo(entry);
                    bool isDir = Directory.Exists(entry);
                    entries.Add(new DirEntry(System.IO.Path.GetFileName(entry), isDir));
                }
                entries.Sort((a, b) => string.Compare(a.Name(), b.Name(), StringComparison.Ordinal));
                return (new Slice<DirEntry>(entries.ToArray()), null);
            }
            catch (Exception ex)
            {
                return (new Slice<DirEntry>(Array.Empty<DirEntry>()), ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("fs.File", "error")]
        public (object?, object?) Open(string name)
        {
            try
            {
                var path = ResolvePath(name);
                if (File.Exists(path))
                {
                    return (new EmbedFile(path), null);
                }
                return (null, $"open {name}: no such file or directory");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }

    internal class EmbedFile
    {
        private readonly FileStream _stream;
        private readonly string _path;

        public EmbedFile(string path)
        {
            _path = path;
            _stream = File.OpenRead(path);
        }

        public (Io.Fs.IGoFileInfo, object?) Stat()
        {
            return (null!, null);
        }

        public (long, object?) Read(Slice<byte> p)
        {
            var buf = new byte[p.Len];
            int n = _stream.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++)
            {
                p[i] = buf[i];
            }
            if (n == 0)
            {
                return (0, "EOF");
            }
            return (n, null);
        }

        public object? Close()
        {
            _stream.Close();
            return null;
        }
    }
}
