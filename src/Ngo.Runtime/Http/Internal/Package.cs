using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http.Internal
{
    [GoPackage("net/http/internal")]
    public static class Package
    {
        [GoVar(Type = "error")]
        public static readonly object ErrLineTooLong = new Exception("header line too long");

        // FlushAfterChunkWriter is a *bufio.Writer wrapper that signals
        // to the http package that it should flush after each chunk is written.
        [GoType("struct", Name = "FlushAfterChunkWriter", Package = "net/http/internal")]
        public class GoFlushAfterChunkWriter
        {
            [GoField(Type = "*bufio.Writer", Embedded = true)] public object? Writer;
        }

        [GoFunc]
        [return: GoReturn("io.Reader")]
        public static object NewChunkedReader([GoParam("io.Reader")] object? r)
        {
            throw new NotImplementedException("net/http/internal.NewChunkedReader not yet implemented");
        }

        [GoFunc]
        [return: GoReturn("io.WriteCloser")]
        public static object NewChunkedWriter([GoParam("io.Writer")] object? w)
        {
            throw new NotImplementedException("net/http/internal.NewChunkedWriter not yet implemented");
        }
    }
}
