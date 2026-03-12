namespace Ngo.Runtime
{
    public sealed class JoinedError
    {
        private readonly object[] _errors;

        public JoinedError(object[] errors)
        {
            _errors = errors;
        }

        public object[] Unwrap() => _errors;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _errors.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_errors[i]);
            }
            return sb.ToString();
        }
    }
}
