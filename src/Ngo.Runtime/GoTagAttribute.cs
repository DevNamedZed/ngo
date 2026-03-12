using System;

namespace Ngo.Runtime
{
    /// <summary>
    /// Custom attribute to store Go struct field tags on emitted fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GoTagAttribute : Attribute
    {
        public string Tag { get; }

        public GoTagAttribute(string tag)
        {
            Tag = tag;
        }
    }
}
