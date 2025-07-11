namespace ImageTools.Utilities
{
    /// <summary>
    /// Wrapper for input streams to simulate truncating of that stream.
    /// </summary>
    public class TruncatedStream : Stream
    {
        private readonly Stream _input;
        private int _byteLimit;

        public TruncatedStream(Stream input, int byteLimit)
        {
            _input = input;
            _byteLimit = byteLimit;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _input.Flush();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override void SetLength(long value){ throw new NotImplementedException(); }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_byteLimit <= 0) return 0;
            if (count < _byteLimit) {
                var actual = _input.Read(buffer, offset, count);
                _byteLimit -= actual;
                return actual;
            }

            var final = _input.Read(buffer, offset, _byteLimit);
            _byteLimit = 0;
            return final;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count){ throw new NotImplementedException(); }

        /// <inheritdoc />
        public override bool CanRead { get => true; }

        /// <inheritdoc />
        public override bool CanSeek { get => false; }

        /// <inheritdoc />
        public override bool CanWrite { get => false; }

        /// <inheritdoc />
        public override long Length { get => _input.Length; }

        /// <inheritdoc />
        public override long Position { get; set; }
    }
}