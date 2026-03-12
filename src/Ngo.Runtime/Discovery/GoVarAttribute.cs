using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a C# static field or property as a Go package-level variable.
    /// Placed on the actual field/property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class GoVarAttribute : Attribute
    {
        /// <summary>Go variable name. Null means use the C# member name.</summary>
        public string? Name { get; set; }

        /// <summary>Override Go type. Only needed when C# type doesn't map automatically.</summary>
        public string? Type { get; set; }
    }
}
