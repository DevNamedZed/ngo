// -----------------------------------------------------------------------
// <copyright file="ObjectFileException.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// Thrown when an object file cannot be read — either because its
    /// container format is unrecognised (wrong magic), because it uses
    /// a format that this stage does not support (PDB, Mach-O, COFF,
    /// ELF32), or because the container-level structure is malformed
    /// (truncated header, section header offset outside the file,
    /// missing section-name string table). The exception carries the
    /// file path so diagnostics point at the specific object file.
    /// </summary>
    public sealed class ObjectFileException : Exception
    {
        public ObjectFileException(string message, string filePath)
            : base(message)
        {
            FilePath = filePath;
        }

        public ObjectFileException(string message, string filePath, Exception innerException)
            : base(message, innerException)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Absolute path to the object file whose read failed. Included
        /// in the message reported to the user but also exposed directly
        /// so callers can log or recover without parsing the message.
        /// </summary>
        public string FilePath { get; }
    }
}
