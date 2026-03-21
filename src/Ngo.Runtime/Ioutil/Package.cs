using System;
using System.IO;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Ioutil
{
    [GoPackage("io/ioutil")]
    public static class Package
    {
        public static (Slice<byte>, object?) ReadAll(object? r)
        {
            if (r is IGoReader reader)
            {
                var (data, err) = Io.GoIo.ReadAll(reader);
                return (data, string.IsNullOrEmpty(err) ? null : (object?)err);
            }
            return (new Slice<byte>(Array.Empty<byte>()), "invalid reader");
        }

        public static (Slice<byte>, object?) ReadFile(string filename)
        {
            try
            {
                var bytes = File.ReadAllBytes(filename);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        public static object? WriteFile(string filename, Slice<byte> data, long perm)
        {
            try
            {
                var bytes = new byte[data.Len];
                for (int i = 0; i < data.Len; i++)
                    bytes[i] = data[i];
                File.WriteAllBytes(filename, bytes);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static object NopCloser(object? r)
        {
            if (r is IGoReader reader)
                return new NopCloserReader(reader);
            return r!;
        }

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) TempDir(string dir, string pattern)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    string.IsNullOrEmpty(dir) ? System.IO.Path.GetTempPath() : dir,
                    pattern + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(path);
                return (path, null);
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // ioutil.ReadDir(dirname string) ([]os.FileInfo, error)
        [GoFunc]
        [return: GoReturn("[]os.FileInfo", "error")]
        public static (Slice<object>, object?) ReadDir(string dirname)
        {
            try
            {
                var entries = Directory.GetFileSystemEntries(dirname);
                Array.Sort(entries, StringComparer.Ordinal);
                var infos = new object[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    infos[i] = Os.GoFileInfo.FromPath(entries[i]);
                }
                return (new Slice<object>(infos), null);
            }
            catch (Exception ex)
            {
                return (new Slice<object>(Array.Empty<object>()), ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("*os.File", "error")]
        public static (object?, object?) TempFile(string dir, string pattern)
        {
            try
            {
                var tempPath = System.IO.Path.Combine(
                    string.IsNullOrEmpty(dir) ? System.IO.Path.GetTempPath() : dir,
                    pattern + Guid.NewGuid().ToString("N").Substring(0, 8));
                File.Create(tempPath).Dispose();
                return (tempPath, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // ioutil.Discard — Writer that discards all data
        [GoVar(Type = "io.Writer")]
        public static readonly Io.DiscardWriter Discard = Io.DiscardWriter.Instance;
    }
}
