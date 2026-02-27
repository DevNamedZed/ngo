// -----------------------------------------------------------------------
// <copyright file="DeferStack.cs" company="Ziad">
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
using System.Collections.Generic;

namespace Ngo.Runtime
{
    /// <summary>
    /// LIFO stack for deferred function calls. Each Go function with defer
    /// statements gets a DeferStack as a local variable. The finally block
    /// calls Execute() to run deferred actions in reverse order.
    /// </summary>
    public sealed class DeferStack
    {
        private readonly Stack<Action> _deferred = new();

        /// <summary>Push a deferred action onto the stack.</summary>
        public void Push(Action action)
        {
            _deferred.Push(action);
        }

        /// <summary>Number of pending deferred actions.</summary>
        public int Count => _deferred.Count;

        /// <summary>
        /// Execute all deferred actions in LIFO order. Called in the finally block.
        /// If a deferred action panics, remaining deferred actions still run
        /// (matching Go behavior where all defers execute even during a panic).
        /// </summary>
        public void Execute()
        {
            GoPanicException? activePanic = null;

            while (_deferred.Count > 0)
            {
                var action = _deferred.Pop();
                try
                {
                    action();
                }
                catch (GoPanicException ex)
                {
                    // A defer panicked — record it but keep running remaining defers
                    activePanic = ex;
                }
            }

            // Re-throw the last panic after all defers have run
            if (activePanic != null)
            {
                throw activePanic;
            }
        }

        /// <summary>
        /// Execute all deferred actions during panic recovery.
        /// Returns the panic value if recover() was called in a deferred function.
        /// </summary>
        public object? ExecuteWithRecover(GoPanicException panic)
        {
            object? recovered = null;
            var previousPanic = GoRecover.CurrentPanic;
            GoRecover.CurrentPanic = panic;

            try
            {
                while (_deferred.Count > 0)
                {
                    var action = _deferred.Pop();
                    try
                    {
                        action();
                    }
                    catch (GoPanicException ex)
                    {
                        // A defer panicked — update the active panic
                        GoRecover.CurrentPanic = ex;
                        recovered = null; // New panic overrides previous recovery
                    }
                }

                recovered = GoRecover.Recovered ? panic.Value : null;
            }
            finally
            {
                GoRecover.CurrentPanic = previousPanic;
                GoRecover.Recovered = false;
            }

            return recovered;
        }
    }
}
