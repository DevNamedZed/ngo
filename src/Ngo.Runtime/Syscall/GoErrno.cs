using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Syscall
{
    [GoType("named", Name = "Errno", Package = "syscall", Underlying = "uintptr")]
    public class GoErrno
    {
        public long Value;

        [GoMethod]
        public string Error()
        {
            return Value switch
            {
                1 => "operation not permitted",
                2 => "no such file or directory",
                3 => "no such process",
                4 => "interrupted system call",
                5 => "input/output error",
                9 => "bad file descriptor",
                11 => "resource temporarily unavailable",
                12 => "cannot allocate memory",
                13 => "permission denied",
                17 => "file exists",
                20 => "not a directory",
                21 => "is a directory",
                22 => "invalid argument",
                28 => "no space left on device",
                32 => "broken pipe",
                36 => "file name too long",
                38 => "function not implemented",
                39 => "directory not empty",
                95 => "operation not supported",
                98 => "address already in use",
                99 => "cannot assign requested address",
                104 => "connection reset by peer",
                110 => "connection timed out",
                111 => "connection refused",
                _ => $"errno {Value}",
            };
        }

        [GoMethod]
        public bool Is(object? target)
        {
            if (target is GoErrno otherErrno)
            {
                return Value == otherErrno.Value;
            }
            if (target is long otherLong)
            {
                return Value == otherLong;
            }
            return false;
        }

        [GoMethod]
        public bool Temporary()
        {
            return Value == 4   // EINTR
                || Value == 11  // EAGAIN
                || Value == 24; // EMFILE
        }

        [GoMethod]
        public bool Timeout()
        {
            return Value == 110; // ETIMEDOUT
        }
    }
}
