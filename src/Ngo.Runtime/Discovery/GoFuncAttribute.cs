using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a C# method as a Go package-level function.
    /// Placed on the actual method. Name defaults to the C# method name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class GoFuncAttribute : Attribute
    {
        /// <summary>Go function name. Null means use the C# method name.</summary>
        public string? Name { get; set; }

        public bool IsVariadic { get; set; }
    }
}
