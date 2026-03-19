using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Safefilepath
{
    /// <summary>
    /// internal/safefilepath — validates and sanitizes file paths.
    /// </summary>
    [GoPackage("internal/safefilepath")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) FromFS(string path)
        {
            // Validate path doesn't contain dangerous elements
            if (path.Contains("..") || System.IO.Path.IsPathRooted(path))
                return ("", "invalid path");
            return (path, null);
        }

        [GoFunc]
        public static bool IsReservedName(string name)
        {
            // Windows reserved names
            var upper = name.ToUpperInvariant();
            return upper is "CON" or "PRN" or "AUX" or "NUL"
                or "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9"
                or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9";
        }
    }
}
