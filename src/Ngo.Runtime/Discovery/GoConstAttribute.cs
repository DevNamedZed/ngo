using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a C# const or static readonly field as a Go constant.
    /// Placed on the actual field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class GoConstAttribute : Attribute
    {
        /// <summary>Go constant name. Null means use the C# field name.</summary>
        public string? Name { get; set; }

        /// <summary>Override Go type. Only needed when C# type doesn't map automatically.</summary>
        public string? Type { get; set; }
    }
}
