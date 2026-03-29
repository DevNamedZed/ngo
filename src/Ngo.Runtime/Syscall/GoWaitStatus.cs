using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Syscall
{
    [GoType("named", Name = "WaitStatus", Package = "syscall", Underlying = "uint32")]
    public class GoWaitStatus
    {
        public long Value;

        // Linux wait status encoding:
        // If WIFEXITED: bits 15-8 = exit status, bits 7-0 = 0
        // If WIFSIGNALED: bits 7-0 = signal number (non-zero), bit 7 = core dump
        // If WIFSTOPPED: bits 15-8 = stop signal, bits 7-0 = 0x7f

        [GoMethod]
        public bool Exited() => ((int)Value & 0x7f) == 0;

        [GoMethod]
        public long ExitStatus() => ((int)Value >> 8) & 0xff;

        [GoMethod]
        public bool Signaled()
        {
            int signal = (int)Value & 0x7f;
            return signal != 0 && signal != 0x7f;
        }

        [GoMethod]
        [return: GoReturn("Signal")]
        public long Signal() => (int)Value & 0x7f;

        [GoMethod]
        public bool CoreDump() => Signaled() && ((int)Value & 0x80) != 0;

        [GoMethod]
        public bool Stopped() => ((int)Value & 0xff) == 0x7f;

        [GoMethod]
        public bool Continued() => (int)Value == 0xffff;

        [GoMethod]
        [return: GoReturn("Signal")]
        public long StopSignal() => ((int)Value >> 8) & 0xff;

        [GoMethod]
        public long TrapCause() => ((int)Value >> 8) & 0xff;
    }
}
