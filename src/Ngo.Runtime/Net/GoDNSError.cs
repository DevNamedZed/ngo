// -----------------------------------------------------------------------
// <copyright file="GoDNSError.cs" company="Ziad">
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

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "DNSError", Package = "net")]
    public class GoDNSError
    {
        [GoField(Name = "UnwrapErr", Type = "error")] public object? UnwrapErr { get; set; }
        [GoField(Name = "Err")] public string Err { get; set; } = "";
        [GoField(Name = "Name")] public string Name { get; set; } = "";
        [GoField(Name = "Server")] public string Server { get; set; } = "";
        [GoField(Name = "IsTimeout")] public bool IsTimeout { get; set; }
        [GoField(Name = "IsTemporary")] public bool IsTemporary { get; set; }
        [GoField(Name = "IsNotFound")] public bool IsNotFound { get; set; }

        [GoMethod]
        public string Error()
        {
            if (IsTimeout)
            {
                return $"lookup {Name} on {Server}: i/o timeout";
            }
            return $"lookup {Name} on {Server}: {Err}";
        }

        [GoMethod]
        public bool Timeout() => IsTimeout;

        [GoMethod]
        public bool Temporary() => IsTemporary;

        [GoMethod]
        public object? Unwrap() => UnwrapErr;
    }
}
