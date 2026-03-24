// -----------------------------------------------------------------------
// <copyright file="GoModuleResolver.cs" company="Ziad">
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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Parses go.mod files and resolves external module dependencies
    /// via the Go module proxy (proxy.golang.org).
    /// </summary>
    public sealed class GoModuleResolver
    {
        private static readonly string CacheRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ngo", "mod", "cache");

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string ProxyUrl = "https://proxy.golang.org";

        private readonly ICompilerLog _log;

        public GoModuleResolver(ICompilerLog? log = null)
        {
            _log = log ?? NullLog.Instance;
        }

        public string? ModuleName { get; private set; }
        public string? ModuleRoot { get; private set; }
        public IReadOnlyDictionary<string, string> Requirements => _requirements;
        public IReadOnlyDictionary<string, string> Replaces => _replaces;

        private readonly Dictionary<string, string> _requirements = new();
        private readonly Dictionary<string, string> _replaces = new();
        private readonly HashSet<string> _loadedModuleGoMods = new();

        /// <summary>
        /// Loads a module's go.mod and merges its requirements into this resolver.
        /// </summary>
        public void LoadTransitiveGoMod(string moduleDir)
        {
            var goModPath = Path.Combine(moduleDir, "go.mod");
            if (!File.Exists(goModPath))
            {
                return;
            }
            if (!_loadedModuleGoMods.Add(goModPath))
            {
                return;
            }
            var lines = File.ReadAllLines(goModPath);
            MergeGoMod(lines);
        }

        /// <summary>
        /// Loads ALL transitive go.mod files reachable from the project's go.mod.
        /// This mirrors the Go compiler's behavior: before compiling anything,
        /// build a complete picture of ALL module versions from the entire
        /// transitive dependency tree.
        /// </summary>
        public void LoadAllTransitiveDependencies()
        {
            var worklist = new Queue<(string module, string version)>();

            // Seed with current project's requirements
            foreach (var req in _requirements)
            {
                worklist.Enqueue((req.Key, req.Value));
            }

            while (worklist.Count > 0)
            {
                var (module, version) = worklist.Dequeue();

                // Find this module's directory in cache
                var moduleDir = GetCachedModuleDir(module, version);
                if (moduleDir == null)
                {
                    // Try to download it
                    moduleDir = DownloadModule(module, version);
                    if (moduleDir == null)
                    {
                        continue;
                    }
                }

                // Load its go.mod
                var goModPath = Path.Combine(moduleDir, "go.mod");
                if (!File.Exists(goModPath) || !_loadedModuleGoMods.Add(goModPath))
                {
                    continue;
                }

                // Parse and merge requirements
                var lines = File.ReadAllLines(goModPath);
                var newRequirements = ParseRequirements(lines);

                foreach (var req in newRequirements)
                {
                    // Go's Minimal Version Selection: pick the HIGHEST version
                    // requested by any module in the dependency graph.
                    if (!_requirements.ContainsKey(req.Key))
                    {
                        _requirements[req.Key] = req.Value;
                        worklist.Enqueue((req.Key, req.Value));
                    }
                    else if (CompareVersions(_requirements[req.Key], req.Value) < 0)
                    {
                        _requirements[req.Key] = req.Value;
                        worklist.Enqueue((req.Key, req.Value));
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the given directory (or a parent) has a go.mod and merges
        /// its requirements into this resolver. Used during dependency discovery
        /// to handle transitive deps from modules that aren't in the project's go.mod.
        /// </summary>
        public void MergeGoModIfPresent(string dir)
        {
            var current = dir;
            for (int i = 0; i < 10; i++)
            {
                if (current == null)
                {
                    break;
                }
                var goModPath = Path.Combine(current, "go.mod");
                if (File.Exists(goModPath))
                {
                    if (_loadedModuleGoMods.Add(goModPath))
                    {
                        var lines = File.ReadAllLines(goModPath);
                        var newReqs = ParseRequirements(lines);
                        foreach (var req in newReqs)
                        {
                            if (!_requirements.ContainsKey(req.Key))
                            {
                                _requirements[req.Key] = req.Value;
                            }
                            else if (CompareVersions(_requirements[req.Key], req.Value) < 0)
                            {
                                _requirements[req.Key] = req.Value;
                            }
                        }
                    }
                    break;
                }
                var parent = Path.GetDirectoryName(current);
                if (parent == current || string.IsNullOrEmpty(parent))
                {
                    break;
                }
                current = parent;
            }
        }

        private static Dictionary<string, string> ParseRequirements(string[] lines)
        {
            var requirements = new Dictionary<string, string>();
            bool inRequireBlock = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                var commentIdx = line.IndexOf("//");
                if (commentIdx >= 0)
                {
                    line = line.Substring(0, commentIdx).Trim();
                }
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line == "require (")
                {
                    inRequireBlock = true;
                    continue;
                }
                if (line == ")" && inRequireBlock)
                {
                    inRequireBlock = false;
                    continue;
                }

                if (inRequireBlock)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        requirements[parts[0]] = parts[1];
                    }
                }
                else if (line.StartsWith("require ") && !line.Contains("("))
                {
                    var parts = line.Substring(8).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        requirements[parts[0]] = parts[1];
                    }
                }
            }
            return requirements;
        }

        private void MergeGoMod(string[] lines)
        {
            bool inRequireBlock = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                var commentIdx = line.IndexOf("//");
                if (commentIdx >= 0)
                {
                    line = line.Substring(0, commentIdx).Trim();
                }
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line == "require (")
                {
                    inRequireBlock = true;
                    continue;
                }
                if (line == ")" && inRequireBlock)
                {
                    inRequireBlock = false;
                    continue;
                }

                if (inRequireBlock)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && !_requirements.ContainsKey(parts[0]))
                    {
                        _requirements[parts[0]] = parts[1];
                    }
                }
                else if (line.StartsWith("require ") && !line.Contains("("))
                {
                    var parts = line.Substring(8).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && !_requirements.ContainsKey(parts[0]))
                    {
                        _requirements[parts[0]] = parts[1];
                    }
                }
            }
        }

        public void LoadGoMod(string dir)
        {
            ModuleName = null;
            ModuleRoot = null;
            _requirements.Clear();
            _replaces.Clear();

            var current = dir;
            while (current != null)
            {
                var goModPath = Path.Combine(current, "go.mod");
                if (File.Exists(goModPath))
                {
                    ModuleRoot = current;
                    ParseGoMod(File.ReadAllLines(goModPath));
                    return;
                }
                var parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
        }

        private void ParseGoMod(string[] lines)
        {
            bool inRequireBlock = false;
            bool inReplaceBlock = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Strip inline comments
                var commentIdx = line.IndexOf("//");
                if (commentIdx >= 0)
                    line = line.Substring(0, commentIdx).Trim();

                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("module "))
                {
                    ModuleName = line.Substring(7).Trim();
                    continue;
                }

                // require ( ... ) block
                if (line == "require (")
                {
                    inRequireBlock = true;
                    continue;
                }

                // replace ( ... ) block
                if (line == "replace (")
                {
                    inReplaceBlock = true;
                    continue;
                }

                if (inRequireBlock)
                {
                    if (line == ")")
                    {
                        inRequireBlock = false;
                        continue;
                    }

                    ParseRequireLine(line);
                    continue;
                }

                if (inReplaceBlock)
                {
                    if (line == ")")
                    {
                        inReplaceBlock = false;
                        continue;
                    }

                    ParseReplaceLine(line);
                    continue;
                }

                // Single-line require
                if (line.StartsWith("require "))
                {
                    ParseRequireLine(line.Substring(8).Trim());
                }

                // Single-line replace
                if (line.StartsWith("replace "))
                {
                    ParseReplaceLine(line.Substring(8).Trim());
                }
            }
        }

        private void ParseRequireLine(string line)
        {
            // Format: "module/path v1.2.3"
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                _requirements[parts[0]] = parts[1];
            }
        }

        private void ParseReplaceLine(string line)
        {
            // Format: "old/module => new/module v1.2.3"
            // Or:     "old/module v1.0.0 => ../local/path"
            // Or:     "old/module => ../local/path"
            var arrowIdx = line.IndexOf("=>");
            if (arrowIdx < 0) return;

            var left = line.Substring(0, arrowIdx).Trim();
            var right = line.Substring(arrowIdx + 2).Trim();

            // Left side: "module" or "module version"
            var leftParts = left.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (leftParts.Length == 0) return;
            var oldModule = leftParts[0];

            // Right side is the replacement (local path or module@version)
            _replaces[oldModule] = right;
        }

        /// <summary>
        /// Given an import path like "github.com/foo/bar/baz", finds which
        /// required module it belongs to (e.g. "github.com/foo/bar").
        /// Returns null if no match.
        /// </summary>
        public ModuleMatch? FindModule(string importPath)
        {
            // Try longest prefix match
            string? bestModule = null;
            string? bestVersion = null;

            foreach (var (mod, ver) in _requirements)
            {
                if (importPath == mod || importPath.StartsWith(mod + "/"))
                {
                    if (bestModule == null || mod.Length > bestModule.Length)
                    {
                        bestModule = mod;
                        bestVersion = ver;
                    }
                }
            }

            if (bestModule != null && bestVersion != null)
            {
                return new ModuleMatch(bestModule, bestVersion);
            }

            return null;
        }

        /// <summary>
        /// Returns the cached directory for a module+version, or null if not cached.
        /// </summary>
        public string? GetCachedModuleDir(string module, string version)
        {
            var dir = GetModuleCachePath(module, version);
            if (Directory.Exists(dir))
            {
                // Verify it has .go files somewhere
                var goFiles = Directory.GetFiles(dir, "*.go", SearchOption.AllDirectories);
                if (goFiles.Length > 0)
                    return dir;
            }
            return null;
        }

        /// <summary>
        /// Downloads a module from the Go module proxy and extracts to cache.
        /// Returns the cache directory path, or null on failure.
        /// </summary>
        public string? DownloadModule(string module, string version)
        {
            var cacheDir = GetModuleCachePath(module, version);

            if (Directory.Exists(cacheDir))
                return cacheDir;

            try
            {
                var escapedModule = EscapeModulePath(module);
                var url = $"{ProxyUrl}/{escapedModule}/@v/{version}.zip";

                var response = Http.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return null;

                var zipBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

                Directory.CreateDirectory(cacheDir);

                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                // Zip entries are prefixed with "module@version/"
                var prefix = $"{module}@{version}/";

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/"))
                        continue; // skip directories

                    string relativePath;
                    if (entry.FullName.StartsWith(prefix))
                        relativePath = entry.FullName.Substring(prefix.Length);
                    else
                        relativePath = entry.FullName;

                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    var destPath = Path.Combine(cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var destDir = Path.GetDirectoryName(destPath)!;

                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    using var entryStream = entry.Open();
                    using var fileStream = File.Create(destPath);
                    entryStream.CopyTo(fileStream);
                }

                return cacheDir;
            }
            catch (Exception ex)
            {
                _log.Warn($"module download failed for '{module}@{version}': {ex.Message}");
                try
                {
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _log.Debug($"cleanup failed for '{cacheDir}': {cleanupEx.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Resolves the directory for a package within a module.
        /// For import "github.com/foo/bar/baz" with module "github.com/foo/bar",
        /// returns the "baz" subdirectory of the module cache.
        /// Checks replace directives first for local path overrides.
        /// </summary>
        public string? ResolvePackageDir(string importPath, string module, string version)
        {
            // Check replace directives first
            if (_replaces.TryGetValue(module, out var replacement))
            {
                var replacePath = ResolveReplacePath(replacement, importPath, module);
                if (replacePath != null)
                {
                    return replacePath;
                }
                // Local path replace failed (dir doesn't exist in module cache download).
                // Fall through to find the module by its original import path in the cache.
            }

            var moduleDir = GetCachedModuleDir(module, version);
            if (moduleDir == null)
            {
                moduleDir = DownloadModule(module, version);
                if (moduleDir == null)
                    return null;
            }

            // The package is at a relative path within the module
            if (importPath == module)
                return moduleDir;

            var relativePath = importPath.Substring(module.Length + 1); // +1 for the /
            var pkgDir = Path.Combine(moduleDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (Directory.Exists(pkgDir))
                return pkgDir;

            return null;
        }

        private string? ResolveReplacePath(string replacement, string importPath, string module)
        {
            // replacement can be:
            // 1. Local path: "../local/path" or "./local/path"
            // 2. Module + version: "other/module v1.2.3"
            var parts = replacement.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1 && (parts[0].StartsWith("./") || parts[0].StartsWith("../") || Path.IsPathRooted(parts[0])))
            {
                // Local path replacement
                var localPath = parts[0];
                if (!Path.IsPathRooted(localPath) && ModuleRoot != null)
                    localPath = Path.GetFullPath(Path.Combine(ModuleRoot, localPath));

                if (importPath == module)
                    return Directory.Exists(localPath) ? localPath : null;

                var relativePath = importPath.Substring(module.Length + 1);
                var pkgDir = Path.Combine(localPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return Directory.Exists(pkgDir) ? pkgDir : null;
            }

            if (parts.Length == 2)
            {
                // Module + version replacement — resolve through cache/download
                var newModule = parts[0];
                var newVersion = parts[1];
                var moduleDir = GetCachedModuleDir(newModule, newVersion)
                                ?? DownloadModule(newModule, newVersion);
                if (moduleDir == null)
                    return null;

                if (importPath == module)
                    return moduleDir;

                var relativePath = importPath.Substring(module.Length + 1);
                var pkgDir = Path.Combine(moduleDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return Directory.Exists(pkgDir) ? pkgDir : null;
            }

            return null;
        }

        /// <summary>
        /// Searches the module cache for a package directory by trying all cached versions
        /// of potential parent modules. For example, "google.golang.org/grpc/internal" will
        /// find the "internal" subdirectory inside any cached "google.golang.org/grpc@vX.Y.Z".
        /// </summary>
        public string? FindInCache(string importPath)
        {
            // Handle replace directives: if the module is replaced with a local path,
            // look for it as a separate module in the cache at the same version.
            foreach (var kvp in _replaces)
            {
                if (importPath == kvp.Key || importPath.StartsWith(kvp.Key + "/"))
                {
                    var replacement = kvp.Value;
                    if (replacement.StartsWith("./") || replacement.StartsWith("../"))
                    {
                        // Local path replace: the replaced module should exist separately in the cache.
                        // Look for it by its original import path (not the local path).
                        // The module cache has it as a separate download.
                        var subPath = importPath.Length > kvp.Key.Length
                            ? importPath.Substring(kvp.Key.Length + 1)
                            : "";
                        var replacedResult = FindModuleInCache(kvp.Key, subPath);
                        if (replacedResult != null)
                        {
                            return replacedResult;
                        }
                    }
                    else
                    {
                        // Module replace: "old => new v1.2.3"
                        var replaceParts = replacement.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (replaceParts.Length >= 1)
                        {
                            var newModule = replaceParts[0];
                            var subPath = importPath.Length > kvp.Key.Length
                                ? importPath.Substring(kvp.Key.Length + 1)
                                : "";
                            var newImport = string.IsNullOrEmpty(subPath) ? newModule : newModule + "/" + subPath;
                            var result = FindInCache(newImport);
                            if (result != null)
                            {
                                return result;
                            }
                        }
                    }
                }
            }

            // Try progressively shorter module paths (longest prefix match).
            // Start at full length to handle import path = module path (no sub-package).
            var parts = importPath.Split('/');
            for (int prefixLen = parts.Length; prefixLen >= 1; prefixLen--)
            {
                var candidateModule = string.Join("/", parts, 0, prefixLen);

                // Check if this module is in the current project's requirements first
                if (_requirements.TryGetValue(candidateModule, out var reqVersion))
                {
                    var reqDir = GetCachedModuleDir(candidateModule, reqVersion);
                    if (reqDir != null)
                    {
                        var subPath = string.Join("/", parts, prefixLen, parts.Length - prefixLen);
                        var pkgDir = Path.Combine(reqDir, subPath.Replace('/', Path.DirectorySeparatorChar));
                        if (Directory.Exists(pkgDir))
                        {
                            return pkgDir;
                        }
                    }
                }

                // Fall back to scanning cache for any version
                var safePath = candidateModule.Replace('/', Path.DirectorySeparatorChar);
                var parentDir = Path.Combine(CacheRoot, Path.GetDirectoryName(safePath) ?? "");

                if (!Directory.Exists(parentDir))
                {
                    continue;
                }

                var moduleDirName = Path.GetFileName(safePath);
                var subPathFallback = string.Join("/", parts, prefixLen, parts.Length - prefixLen);

                string? bestMatch = null;
                string? bestVersion = null;
                foreach (var dir in Directory.GetDirectories(parentDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(moduleDirName + "@"))
                    {
                        var pkgDir = Path.Combine(dir, subPathFallback.Replace('/', Path.DirectorySeparatorChar));
                        if (Directory.Exists(pkgDir))
                        {
                            var version = dirName.Substring(moduleDirName.Length + 1);
                            if (bestMatch == null || CompareVersions(version, bestVersion!) > 0)
                            {
                                bestMatch = pkgDir;
                                bestVersion = version;
                            }
                        }
                    }
                }
                if (bestMatch != null)
                {
                    return bestMatch;
                }
            }
            return null;
        }

        private string? FindModuleInCache(string modulePath, string subPath)
        {
            var safePath = modulePath.Replace('/', Path.DirectorySeparatorChar);
            var parentDir = Path.Combine(CacheRoot, Path.GetDirectoryName(safePath) ?? "");
            if (!Directory.Exists(parentDir))
            {
                return null;
            }

            var moduleDirName = Path.GetFileName(safePath);
            string? bestMatch = null;
            string? bestVersion = null;
            foreach (var dir in Directory.GetDirectories(parentDir))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith(moduleDirName + "@"))
                {
                    var pkgDir = string.IsNullOrEmpty(subPath)
                        ? dir
                        : Path.Combine(dir, subPath.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(pkgDir))
                    {
                        var version = dirName.Substring(moduleDirName.Length + 1);
                        if (bestMatch == null || CompareVersions(version, bestVersion!) > 0)
                        {
                            bestMatch = pkgDir;
                            bestVersion = version;
                        }
                    }
                }
            }
            return bestMatch;
        }

        private static int CompareVersions(string a, string b)
        {
            // Simple semver comparison: v1.29.1 vs v1.69.2
            var aParts = a.TrimStart('v').Split('.', '-');
            var bParts = b.TrimStart('v').Split('.', '-');
            for (int i = 0; i < System.Math.Max(aParts.Length, bParts.Length); i++)
            {
                var aVal = i < aParts.Length && int.TryParse(aParts[i], out var av) ? av : 0;
                var bVal = i < bParts.Length && int.TryParse(bParts[i], out var bv) ? bv : 0;
                if (aVal != bVal)
                {
                    return aVal.CompareTo(bVal);
                }
            }
            return string.Compare(a, b, StringComparison.Ordinal);
        }

        private static string GetModuleCachePath(string module, string version)
        {
            // Use @ separator like Go's module cache
            var safePath = module.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(CacheRoot, safePath + "@" + version);
        }

        /// <summary>
        /// Escapes uppercase letters in module path for the proxy URL.
        /// Go module proxy requires uppercase X → !x.
        /// </summary>
        internal static string EscapeModulePath(string modulePath)
        {
            var sb = new StringBuilder(modulePath.Length);
            foreach (var c in modulePath)
            {
                if (char.IsUpper(c))
                {
                    sb.Append('!');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
