using System.IO;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Thin wrapper over <see cref="CgoSymbolCatalogSerializer"/>
    /// that pins the on-disk filename (<c>catalog.json</c>) and
    /// centralises the load/save convention so the DWARF reader,
    /// the PDB reader, and the build-cgo orchestrator do not each
    /// re-invent path construction. The cache directory itself
    /// comes from the existing anchor-probe cache layout produced
    /// by <see cref="CgoCompiler"/>; this helper does not invent
    /// a new location.
    /// </summary>
    public static class CgoSymbolCatalogCache
    {
        public const string CatalogFileName = "catalog.json";

        /// <summary>
        /// Attempts to load a cached catalog from
        /// <paramref name="cacheDirectory"/>. Returns <c>null</c>
        /// when the file does not exist — that is the one
        /// tolerated outcome, because a missing catalog means
        /// "cache miss, go run the reader". Any other failure
        /// (unreadable file, malformed JSON, wrong version) is
        /// surfaced as an exception; we never silently fall back
        /// to regenerating because that would mask reader bugs.
        /// </summary>
        public static CgoSymbolCatalog? TryLoad(string cacheDirectory)
        {
            string path = Path.Combine(cacheDirectory, CatalogFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            return CgoSymbolCatalogSerializer.Deserialize(File.ReadAllText(path));
        }

        public static void Save(string cacheDirectory, CgoSymbolCatalog catalog)
        {
            Directory.CreateDirectory(cacheDirectory);
            string path = Path.Combine(cacheDirectory, CatalogFileName);
            File.WriteAllText(path, CgoSymbolCatalogSerializer.Serialize(catalog));
        }
    }
}
