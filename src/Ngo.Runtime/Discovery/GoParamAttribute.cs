using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Overrides the Go type for a C# parameter.
    /// Only needed when the Go type differs from the C# type mapping
    /// (e.g. C# object? → Go interface{}, C# object? → Go error).
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class GoParamAttribute : Attribute
    {
        /// <summary>Go type string (e.g. "error", "interface{}", "[]byte").</summary>
        public string Type { get; }

        public GoParamAttribute(string type)
        {
            Type = type;
        }
    }
}
