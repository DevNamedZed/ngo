// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Path
{
    [GoPackage("path")]
    public static class Package
    {
        [GoFunc]
        public static string Base(string path)
        {
            if (path == "") return ".";
            // Strip trailing slashes
            while (path.Length > 0 && path[path.Length - 1] == '/')
                path = path.Substring(0, path.Length - 1);
            if (path == "") return "/";
            int i = path.LastIndexOf('/');
            if (i >= 0) path = path.Substring(i + 1);
            return path;
        }

        [GoFunc]
        public static string Dir(string path)
        {
            int i = path.LastIndexOf('/');
            if (i < 0) return ".";
            var dir = path.Substring(0, i);
            // Strip trailing slashes
            while (dir.Length > 1 && dir[dir.Length - 1] == '/')
                dir = dir.Substring(0, dir.Length - 1);
            if (dir == "") return "/";
            return dir;
        }

        [GoFunc]
        public static string Ext(string path)
        {
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] == '.') return path.Substring(i);
                if (path[i] == '/') break;
            }
            return "";
        }

        [GoFunc(IsVariadic = true)]
        public static string Join(params string[] elem)
        {
            var result = "";
            foreach (var e in elem)
            {
                if (e == "") continue;
                if (result == "") result = e;
                else result = result.TrimEnd('/') + "/" + e.TrimStart('/');
            }
            return result;
        }

        [GoFunc]
        public static string Clean(string path)
        {
            if (path == "") return ".";
            return path;
        }

        [GoFunc]
        public static bool IsAbs(string path)
        {
            return path.Length > 0 && path[0] == '/';
        }

        // --- Stubs for exports in PackageRegistry but missing from runtime ---

        [GoFunc]
        [return: GoReturn("string", "string")]
        public static (string, string) Split(string path)
        {
            int i = path.LastIndexOf('/');
            if (i < 0) return ("", path);
            return (path.Substring(0, i + 1), path.Substring(i + 1));
        }

        [GoFunc]
        [return: GoReturn("bool", "error")]
        public static (bool, object?) Match(string pattern, string name)
        {
            // stub: path.Match(pattern, name string) (matched bool, err error)
            // Simple glob matching for basic cases
            try
            {
                return (SimpleGlobMatch(pattern, name), null);
            }
            catch (Exception)
            {
                return (false, ErrBadPattern);
            }
        }

        [GoVar(Type = "error")]
        public static readonly object? ErrBadPattern = "syntax error in pattern";

        private static bool SimpleGlobMatch(string pattern, string name)
        {
            return SimpleGlobMatchInner(pattern, 0, name, 0);
        }

        private static bool SimpleGlobMatchInner(string pattern, int pi, string name, int ni)
        {
            while (pi < pattern.Length)
            {
                if (pattern[pi] == '*')
                {
                    pi++;
                    if (pi >= pattern.Length) return true;
                    for (int i = ni; i <= name.Length; i++)
                    {
                        if (SimpleGlobMatchInner(pattern, pi, name, i))
                            return true;
                    }
                    return false;
                }
                else if (pattern[pi] == '?')
                {
                    if (ni >= name.Length) return false;
                    pi++;
                    ni++;
                }
                else
                {
                    if (ni >= name.Length || pattern[pi] != name[ni])
                        return false;
                    pi++;
                    ni++;
                }
            }
            return ni == name.Length;
        }
    }
}
