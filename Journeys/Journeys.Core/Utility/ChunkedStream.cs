using System.IO;

namespace Journeys.Core.Utility;

public class ChunkedStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _startByte;
    private readonly long _endByte;
    private long _position;

    public ChunkedStream(Stream baseStream, long startByte, long endByte)
    {
        _baseStream = baseStream;
        _startByte = startByte;
        _endByte = endByte;
        _position = 0;
        
        // Seek to start position
        _baseStream.Seek(startByte, SeekOrigin.Begin);
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _endByte - _startByte + 1;
    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            
            _position = value;
            _baseStream.Position = _startByte + value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= Length)
            return 0;

        var remainingBytes = Length - _position;
        var bytesToRead = Math.Min(count, (int)remainingBytes);
        
        var bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
        _position += bytesRead;
        
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPosition = offset;
                break;
            case SeekOrigin.Current:
                newPosition = _position + offset;
                break;
            case SeekOrigin.End:
                newPosition = Length + offset;
                break;
            default:
                throw new ArgumentException("Invalid seek origin");
        }

        if (newPosition < 0 || newPosition > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _position = newPosition;
        _baseStream.Seek(_startByte + newPosition, SeekOrigin.Begin);
        return newPosition;
    }

    public override void Flush()
    {
        _baseStream.Flush();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("Cannot set length on chunked stream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Cannot write to chunked stream");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream?.Dispose();
        }
        base.Dispose(disposing);
    }
} 