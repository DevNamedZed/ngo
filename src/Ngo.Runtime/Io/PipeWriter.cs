// -----------------------------------------------------------------------
// <copyright file="PipeWriter.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Io
{
    /// <summary>io.PipeWriter - the write half of a pipe.</summary>
    [GoType("struct", Package = "io", Name = "PipeWriter")]
    public sealed class PipeWriter : IGoWriter, IGoCloser
    {
        private readonly PipeBuffer _pipe;

        internal PipeWriter(PipeBuffer pipe)
        {
            _pipe = pipe;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Write(Slice<byte> p)
        {
            return _pipe.Write(p);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Close()
        {
            _pipe.Close(GoIo.EOF);
            return "";
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string CloseWithError([GoParam("error")] string err)
        {
            _pipe.Close(err != "" ? err : GoIo.EOF);
            return "";
        }
    }
}
