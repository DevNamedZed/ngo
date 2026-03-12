namespace Ngo.Runtime.Flag
{
    /// <summary>
    /// String pointer wrapper for flag package (Ptr&lt;T&gt; requires struct).
    /// Supports deref via Value property, same as Ptr&lt;T&gt;.
    /// </summary>
    public class FlagStringPtr
    {
        public string Value;

        public FlagStringPtr(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;
    }
}
