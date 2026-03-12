// -----------------------------------------------------------------------
// <copyright file="GoTimer.cs" company="Ziad">
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

namespace Ngo.Runtime.Time
{
    // time.Timer — struct type
    [GoType("struct", Name = "Timer", Package = "time")]
    public sealed class GoTimer
    {
        [GoField(Name = "C", Type = "<-chan Time")]
        public Channel<GoTimeValue> C { get; set; } = new Channel<GoTimeValue>(1);

        [GoMethod]
        public bool Stop()
        {
            // Stub
            return false;
        }

        [GoMethod]
        public bool Reset([GoParam("Duration")] long d)
        {
            // Stub
            return false;
        }

        public GoTimer(long duration)
        {
            // Stub: timer with given duration in nanoseconds
        }
    }
}
