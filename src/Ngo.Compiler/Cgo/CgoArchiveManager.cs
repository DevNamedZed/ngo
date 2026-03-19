using System;
using System.IO;
using System.Text.Json;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Manages CGo compilation artifacts alongside .ngo package archives.
    /// Stores native library references and probe results as a companion .ngo.cgo file.
    ///
    /// Layout:
    ///   ~/.ngo/cache/pkg/
    ///   ├── mypackage.ngo         (Go metadata + IL)
    ///   ├── mypackage.ngo.cgo     (CGo metadata: lib hash, probe results)
    ///   └── cgo/                   (static libraries by hash, linked at build time)
    ///       └── {hash}/
    ///           └── libcgo_{pkg}.a
    /// </summary>
    public static class CgoArchiveManager
    {
        /// <summary>
        /// Save CGo compilation results alongside a .ngo archive.
        /// </summary>
        public static void SaveCgoMetadata(string ngoArchivePath, CgoCompilationResult result)
        {
            if (result == null || !result.Success || result.NativeLibraryPath == null)
            {
                return;
            }

            string cgoMetaPath = ngoArchivePath + ".cgo";
            var metadata = new CgoArchiveMetadata
            {
                NativeLibraryPath = result.NativeLibraryPath,
                CacheHit = result.CacheHit,
                CompilerKind = result.CompilerInfo?.Kind.ToString() ?? "",
                CompilerVersion = result.CompilerInfo?.Version ?? "",
            };

            // Copy probe results
            if (result.ProbeResult != null)
            {
                foreach (var kv in result.ProbeResult.TypeSizes)
                {
                    metadata.TypeSizes[kv.Key] = kv.Value;
                }
                foreach (var kv in result.ProbeResult.EnumValues)
                {
                    metadata.EnumValues[kv.Key] = kv.Value;
                }
            }

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cgoMetaPath, json);
        }

        /// <summary>
        /// Load CGo metadata from a .ngo.cgo companion file.
        /// Returns null if no CGo metadata exists.
        /// </summary>
        public static CgoArchiveMetadata? LoadCgoMetadata(string ngoArchivePath)
        {
            string cgoMetaPath = ngoArchivePath + ".cgo";
            if (!File.Exists(cgoMetaPath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(cgoMetaPath);
                return JsonSerializer.Deserialize<CgoArchiveMetadata>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a cached CGo native library exists and is valid.
        /// </summary>
        public static bool HasValidNativeLibrary(string ngoArchivePath)
        {
            var metadata = LoadCgoMetadata(ngoArchivePath);
            if (metadata == null)
            {
                return false;
            }
            return !string.IsNullOrEmpty(metadata.NativeLibraryPath) &&
                   File.Exists(metadata.NativeLibraryPath);
        }

        /// <summary>
        /// Get the native library path for a cached CGo package.
        /// </summary>
        public static string? GetNativeLibraryPath(string ngoArchivePath)
        {
            var metadata = LoadCgoMetadata(ngoArchivePath);
            if (metadata?.NativeLibraryPath != null && File.Exists(metadata.NativeLibraryPath))
            {
                return metadata.NativeLibraryPath;
            }
            return null;
        }
    }

    /// <summary>
    /// Serializable metadata for CGo compilation stored alongside .ngo archives.
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
