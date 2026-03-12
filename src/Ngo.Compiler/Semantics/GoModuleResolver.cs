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
        private readonly Dictionary<string, string> _replaces = new(); // module → local path or module@version

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
                    return replacePath;
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
