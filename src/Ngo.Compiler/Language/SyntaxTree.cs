// -----------------------------------------------------------------------
// <copyright file="SyntaxTree.cs" company="Ziad">
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

using System.Collections.Generic;
using System.Linq;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Language
{
    public sealed class SyntaxTree
    {
        private SyntaxTree(
            SourceFileSyntax root,
            string sourceText,
            string sourcePath,
            IReadOnlyList<CompileError> errors)
        {
            Root = root;
            SourceText = sourceText;
            SourcePath = sourcePath;
            Errors = errors;
        }

        public SourceFileSyntax Root { get; }

        public string SourceText { get; }

        /// <summary>
        /// Absolute path of the file this tree was parsed from, or the
        /// empty string when the tree was built from an in-memory source
        /// with no on-disk location (e.g. synthetic test inputs). Callers
        /// that need the source directory — notably
        /// <see cref="Ngo.Compiler.Cgo.CgoPreambleExtractor"/> for the
        /// probe's <c>-I</c> argument — must populate this through the
        /// two-argument <see cref="Parse(string, string)"/> overload.
        /// </summary>
        public string SourcePath { get; }

        public IReadOnlyList<CompileError> Errors { get; }

        public bool HasErrors => Errors.Any(e => e.Severity == ErrorSeverity.Error);

        public static SyntaxTree Parse(string sourceText)
        {
            return Parse(sourceText, string.Empty);
        }

        public static SyntaxTree Parse(string sourceText, string sourcePath)
        {
            var parser = new Parser(sourceText);
            var root = parser.ParseSourceFile();
            return new SyntaxTree(root, sourceText, sourcePath, parser.Errors);
        }
    }
}
