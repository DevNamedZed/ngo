using System;

namespace Ngo.Runtime.Testing
{
    public class TestFailException : Exception
    {
        public TestFailException(string message) : base(message) { }
    }
}
