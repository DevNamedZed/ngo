// -----------------------------------------------------------------------
// <copyright file="GoTime.cs" company="Ziad">
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

using System.Threading;

namespace Ngo.Runtime
{
    public static class GoTime
    {
        // Go's time.Duration is int64 nanoseconds
        // time.Sleep takes a Duration (nanoseconds)
        public static void Sleep(long nanoseconds)
        {
            int ms = (int)(nanoseconds / 1_000_000);
            if (ms > 0)
            {
                Thread.Sleep(ms);
            }
        }

        // Duration constants (nanoseconds)
        public const long Nanosecond = 1;
        public const long Microsecond = 1000;
        public const long Millisecond = 1_000_000;
        public const long Second = 1_000_000_000;
    }
}
