using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a C# method as a Go method on a type.
    /// Placed on the actual method definition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class GoMethodAttribute : Attribute
    {
        /// <summary>Go method name. Null means use the C# method name.</summary>
        public string? Name { get; set; }

        public bool IsVariadic { get; set; }
    }
}
