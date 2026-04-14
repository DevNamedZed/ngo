using System;
using System.IO;
using System.Text.Json;
using Ngo.Compiler.Archive;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Manages CGo compilation artifacts inside .ngo ZIP archives.
    /// Native libraries and probe results are stored in the native/ directory
    /// within the ZIP archive.
    /// </summary>
    public static class CgoArchiveManager
    {
        /// <summary>
        /// Save CGo compilation results into a .ngo ZIP archive.
        /// </summary>
        public static void SaveCgoMetadata(string ngoArchivePath, CgoCompilationResult result)
        {
            if (result == null || result.NativeLibraryPath == null)
            {
                return;
            }

            if (!File.Exists(ngoArchivePath))
            {
                return;
            }

            NgoArchive.WriteCgoData(ngoArchivePath, result.NativeLibraryPath, result.ProbeResult);
        }

        /// <summary>
        /// Check if a cached CGo native library exists and is valid.
        /// </summary>
        public static bool HasValidNativeLibrary(string ngoArchivePath)
        {
            var libPath = GetNativeLibraryPath(ngoArchivePath);
            return libPath != null;
        }

        /// <summary>
        /// Get the native library path for a cached CGo package.
        /// Extracts the library from the ZIP archive to a temp directory.
        /// </summary>
        public static string? GetNativeLibraryPath(string ngoArchivePath)
        {
            return NgoArchive.ReadCgoNativeLibrary(ngoArchivePath);
        }
    }

    /// <summary>
    /// Serializable metadata for CGo compilation.
    /// </summary>
    public class CgoArchiveMetadata
    {
        public string NativeLibraryPath { get; set; } = "";
        public bool CacheHit { get; set; }
        public string CompilerKind { get; set; } = "";
        public string CompilerVersion { get; set; } = "";
        public System.Collections.Generic.Dictionary<string, long> TypeSizes { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, long> EnumValues { get; set; } = new();
    }
}
