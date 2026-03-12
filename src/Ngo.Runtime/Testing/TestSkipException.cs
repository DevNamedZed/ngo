using System;

namespace Ngo.Runtime.Testing
{
    public class TestSkipException : Exception
    {
        public TestSkipException(string message) : base(message) { }
    }
}
