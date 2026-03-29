// -----------------------------------------------------------------------
// <copyright file="GoSyscallError.cs" company="Ziad">
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

namespace Ngo.Runtime.Os
{
    /// <summary>
    /// Represents Go's os.SyscallError struct.
    /// </summary>
    [GoType("struct", Name = "SyscallError", Package = "os")]
    public sealed class GoSyscallError
    {
        [GoField(Name = "Syscall")] public string Syscall { get; }
        [GoField(Name = "Err", Type = "error")] public object Err { get; }

        public GoSyscallError(string syscall, object err)
        {
            Syscall = syscall;
            Err = err;
        }

        [GoMethod]
        public string Error() => $"{Syscall}: {Err}";
        [GoMethod]
        public bool Timeout()
        {
            var timeoutMethod = Err?.GetType().GetMethod("Timeout");
            if (timeoutMethod != null)
            {
                return timeoutMethod.Invoke(Err, null) is true;
            }
            return false;
        }
        [GoMethod]
        [return: GoReturn("error")]
        public object Unwrap() => Err;

        public override string ToString() => Error();
    }
}
