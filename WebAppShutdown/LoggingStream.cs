using System.IO;
using Microsoft.Extensions.Logging;

namespace WebAppShutdown
{
    internal class LoggingStream : Stream
    {
        private ILogger<ControlShutdown> logger;

        public LoggingStream(ILogger<ControlShutdown> logger)
        {
            this.logger = logger;
        }

        public override void Close()
        {
            logger.LogInformation("Stream Close() called");
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => Position;

        public override long Position { get; set; }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            logger.LogInformation("Stream received {Count} bytes", count);
        }
    }
}