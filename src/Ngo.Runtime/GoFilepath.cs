// -----------------------------------------------------------------------
// <copyright file="GoFilepath.cs" company="Ziad">
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

using System.IO;

namespace Ngo.Runtime
{
    /// <summary>
    /// Runtime support for Go's path/filepath package.
    /// </summary>
    public static class GoFilepath
    {
        /// <summary>filepath.Join(elem ...string) string</summary>
        public static string Join(params string[] elems)
        {
            return Path.Combine(elems);
        }

        /// <summary>filepath.Dir(path string) string</summary>
        public static string Dir(string path)
        {
            return Path.GetDirectoryName(path) ?? ".";
        }

        /// <summary>filepath.Base(path string) string</summary>
        public static string Base(string path)
        {
            var name = Path.GetFileName(path);
            return name == "" ? "." : name;
        }

        /// <summary>filepath.Ext(path string) string</summary>
        public static string Ext(string path)
        {
            return Path.GetExtension(path);
        }

        /// <summary>filepath.Clean(path string) string</summary>
        public static string Clean(string path)
        {
            return Path.GetFullPath(path);
        }

        /// <summary>filepath.IsAbs(path string) bool</summary>
        public static bool IsAbs(string path)
        {
            return Path.IsPathRooted(path);
        }

        /// <summary>filepath.Abs(path string) (string, error)</summary>
        public static (string, string) Abs(string path)
        {
            return (Path.GetFullPath(path), "");
        }
    }
}
