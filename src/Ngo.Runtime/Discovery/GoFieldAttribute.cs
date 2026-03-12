using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a C# field or property as a Go struct field.
    /// Placed on the actual field/property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class GoFieldAttribute : Attribute
    {
        /// <summary>Go field name. Null means use the C# member name.</summary>
        public string? Name { get; set; }

        /// <summary>Override Go type. Only needed when C# type doesn't map automatically.</summary>
        public string? Type { get; set; }

        /// <summary>True if this is an embedded (anonymous) field in Go.</summary>
        public bool Embedded { get; set; }
    }
}
