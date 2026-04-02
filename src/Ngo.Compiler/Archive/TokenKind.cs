// -----------------------------------------------------------------------
// <copyright file="TokenKind.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// Token kinds for IL metadata token references in .ngo archives.
    /// </summary>
    public static class TokenKind
    {
        public const byte Type = 0;
        public const byte Method = 1;
        public const byte Field = 2;
        public const byte String = 3;
    }
}
