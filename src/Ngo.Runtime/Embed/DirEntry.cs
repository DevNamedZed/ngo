using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Embed
{
    [GoType("struct", Name = "DirEntry", Package = "embed")]
    public class DirEntry
    {
        private readonly string _name;
        private readonly bool _isDir;

        public DirEntry() : this("", false) { }

        internal DirEntry(string name, bool isDir)
        {
            _name = name;
            _isDir = isDir;
        }

        [GoMethod]
        public string Name() => _name;

        [GoMethod]
        public bool IsDir() => _isDir;

        [GoMethod]
        [return: GoReturn("io/fs.FileMode")]
        public Io.Fs.GoFileMode Type()
        {
            if (_isDir)
            {
                return new Io.Fs.GoFileMode(Io.Fs.Package.ModeDir);
            }
            return new Io.Fs.GoFileMode(0);
        }

        [GoMethod]
        [return: GoReturn("io/fs.FileInfo", "error")]
        public (object?, object?) Info()
        {
            return (null, null);
        }
    }
}
