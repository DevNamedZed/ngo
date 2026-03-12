namespace Ngo.Runtime
{
    public sealed class WrappedError
    {
        public string Message { get; }
        public object? Inner { get; }

        public WrappedError(string message, object? inner)
        {
            Message = message;
            Inner = inner;
        }

        public override string ToString() => Message;
    }
}
