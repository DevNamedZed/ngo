// -----------------------------------------------------------------------
// <copyright file="IObjectFileReader.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// Reads debug sections out of an object-file container. One
    /// implementation per container format — ELF, Mach-O, COFF — but
    /// only ELF64 is implemented in this stage. Implementations must
    /// be stateless: each <see cref="Read"/> call is independent and
    /// a single instance is safe to reuse across multiple files.
    /// </summary>
    public interface IObjectFileReader
    {
        /// <summary>
        /// Parse the object file at <paramref name="objectFilePath"/>
        /// and return its pointer size and all debug sections. Throws
        /// <see cref="ObjectFileException"/> on any container-level
        /// error — unrecognised magic, truncated header, malformed
        /// section table, missing section-name string table.
        /// </summary>
        ObjectFileContents Read(string objectFilePath);
    }
}
