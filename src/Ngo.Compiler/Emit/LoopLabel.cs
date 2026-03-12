// -----------------------------------------------------------------------
// <copyright file="LoopLabel.cs" company="Ziad">
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

using System.Reflection.Emit;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Holds the break and continue IL labels for a loop construct.
    /// Used by EmitContext to track the label stack for nested loops.
    /// </summary>
    public sealed class LoopLabel
    {
        public LoopLabel(Label breakLabel, Label continueLabel)
        {
            BreakLabel = breakLabel;
            ContinueLabel = continueLabel;
        }

        public Label BreakLabel { get; }

        public Label ContinueLabel { get; }
    }
}
