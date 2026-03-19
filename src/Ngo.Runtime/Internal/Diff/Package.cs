using System;
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Diff
{
    /// <summary>
    /// internal/diff — unified diff generation.
    /// Used by go/format.
    /// </summary>
    [GoPackage("internal/diff")]
    public static class Package
    {
        // func Diff(oldName string, old []byte, newName string, new_ []byte) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> Diff(string oldName, Slice<byte> old, string newName, Slice<byte> new_)
        {
            var oldLines = SplitLines(old);
            var newLines = SplitLines(new_);

            if (LinesEqual(oldLines, newLines))
                return default;

            var sb = new StringBuilder();
            sb.AppendLine($"--- {oldName}");
            sb.AppendLine($"+++ {newName}");

            // Simple line-by-line diff with context
            int i = 0, j = 0;
            while (i < oldLines.Length || j < newLines.Length)
            {
                if (i < oldLines.Length && j < newLines.Length && oldLines[i] == newLines[j])
                {
                    sb.AppendLine($" {oldLines[i]}");
                    i++; j++;
                }
                else
                {
                    sb.AppendLine($"@@ -{i + 1} +{j + 1} @@");
                    while (i < oldLines.Length && (j >= newLines.Length || oldLines[i] != newLines[j]))
                    {
                        sb.AppendLine($"-{oldLines[i]}");
                        i++;
                    }
                    while (j < newLines.Length && (i >= oldLines.Length || oldLines[i] != newLines[j]))
                    {
                        sb.AppendLine($"+{newLines[j]}");
                        j++;
                    }
                }
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return new Slice<byte>(bytes);
        }

        private static string[] SplitLines(Slice<byte> data)
        {
            if (data.Len == 0) return Array.Empty<string>();
            var bytes = new byte[data.Len];
            for (int i = 0; i < data.Len; i++) bytes[i] = data[i];
            var s = System.Text.Encoding.UTF8.GetString(bytes);
            return s.Split('\n');
        }

        private static bool LinesEqual(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
