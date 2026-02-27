// -----------------------------------------------------------------------
// <copyright file="GoRecover.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    /// <summary>
    /// Implements Go's recover() mechanism. Thread-local state tracks
    /// whether we're inside a deferred function running during a panic.
    /// </summary>
    public static class GoRecover
    {
        [System.ThreadStatic]
        internal static GoPanicException? CurrentPanic;

        [System.ThreadStatic]
        internal static bool Recovered;

        /// <summary>
        /// recover() — returns the panic value if called directly inside a deferred function
        /// during a panic, otherwise returns null.
        /// </summary>
        public static object? Recover()
        {
            if (CurrentPanic != null)
            {
                Recovered = true;
                return CurrentPanic.Value;
            }

            return null;
        }
    }
}
