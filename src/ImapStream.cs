using System;
using System.IO;
using System.Text;

namespace ImapSyncWin
{
    /// <summary>
    /// Thread-safe, unified IMAP stream reader/writer.
    /// Eliminates StreamReader buffering conflicts by maintaining a single internal
    /// byte buffer for both line-based protocol commands and exact-length binary literals.
    /// </summary>
    public class ImapStream : IDisposable
    {
        private Stream _stream;
        private readonly byte[] _buffer;
        private int _bufferPos;
        private int _bufferCount;

        public ImapStream(Stream stream, int bufferSize = 65536)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            _stream = stream;
            _buffer = new byte[Math.Max(8192, bufferSize)];
            _bufferPos = 0;
            _bufferCount = 0;
        }

        /// <summary>
        /// Reads a single line from the IMAP stream ending with \n, stripping trailing \r.
        /// Returns null on end of stream.
        /// </summary>
        public string ReadLine()
        {
            using (var ms = new MemoryStream(256))
            {
                while (true)
                {
                    if (_bufferPos >= _bufferCount)
                    {
                        _bufferPos = 0;
                        _bufferCount = _stream.Read(_buffer, 0, _buffer.Length);
                        if (_bufferCount <= 0)
                        {
                            if (ms.Length > 0) break;
                            return null;
                        }
                    }

                    byte b = _buffer[_bufferPos++];
                    if (b == 0x0A) // '\n'
                    {
                        break;
                    }
                    ms.WriteByte(b);
                }

                byte[] lineBytes = ms.ToArray();
                int len = lineBytes.Length;
                if (len > 0 && lineBytes[len - 1] == 0x0D) // '\r'
                {
                    len--;
                }
                return Encoding.UTF8.GetString(lineBytes, 0, len);
            }
        }

        /// <summary>
        /// Reads exactly length bytes of literal data.
        /// Drains any bytes already present in the read buffer first, then reads
        /// the remaining bytes directly from the underlying network stream.
        /// </summary>
        public byte[] ReadExactBytes(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length", "Length cannot be negative.");
            if (length == 0) return new byte[0];

            byte[] result = new byte[length];
            int totalRead = 0;

            // 1. Drain bytes already buffered in _buffer
            int available = _bufferCount - _bufferPos;
            if (available > 0)
            {
                int toCopy = Math.Min(available, length);
                Buffer.BlockCopy(_buffer, _bufferPos, result, 0, toCopy);
                _bufferPos += toCopy;
                totalRead += toCopy;
            }

            // 2. Read the remainder directly from the underlying stream
            while (totalRead < length)
            {
                int read = _stream.Read(result, totalRead, length - totalRead);
                if (read <= 0)
                {
                    throw new EndOfStreamException(string.Format(
                        "Connection terminated prematurely while reading IMAP literal ({0} of {1} bytes read).",
                        totalRead, length));
                }
                totalRead += read;
            }

            return result;
        }

        /// <summary>
        /// Writes raw bytes to the underlying stream and flushes.
        /// </summary>
        public void Write(byte[] data, int offset, int count)
        {
            if (_stream == null) throw new ObjectDisposedException("ImapStream");
            _stream.Write(data, offset, count);
            _stream.Flush();
        }

        /// <summary>
        /// Writes a UTF-8 command line ending with \r\n and flushes.
        /// </summary>
        public void WriteLine(string line)
        {
            if (_stream == null) throw new ObjectDisposedException("ImapStream");
            byte[] bytes = Encoding.UTF8.GetBytes(line + "\r\n");
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }

        public void Dispose()
        {
            try
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
            }
            catch { }
        }
    }
}
