using System.Buffers.Binary;
using System.Text;
namespace Sona.Core;

public readonly record struct SpeechChunk(int SampleRate, int Offset, int Bytes);

// Long native offline requests can omit a tail. Bound independent requests
// Bound independent requests at quiet points and cover every PCM sample once.
public static class SpeechAudio
{
    public static IReadOnlyList<SpeechChunk> Plan(byte[] wav)
    {
        if (wav.Length is < 44 or > 8 * 1024 * 1024 || !Tag(wav, 0, "RIFF") || !Tag(wav, 8, "WAVE")) throw new IOException("Parakeet requires a bounded PCM WAV recording.");
        var end = (long)BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(4, 4)) + 8;
        if (end > wav.Length || end < 44) throw new IOException("The WAV recording is incomplete.");
        var rate = 0; var dataOffset = -1; var dataBytes = 0; var count = 0;
        for (long cursor = 12; cursor < end;)
        {
            if (++count > 256 || cursor + 8 > end) throw new IOException("The WAV chunk headers are invalid or excessive.");
            var index = checked((int)cursor); var length = BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(index + 4, 4));
            var next = cursor + 8 + length + (length & 1);
            if (next > end) throw new IOException("The WAV chunk exceeds the recording.");
            if (Tag(wav, index, "fmt "))
            {
                if (rate != 0 || length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(index + 8, 2)) != 1 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(index + 10, 2)) != 1 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(index + 20, 2)) != 2 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(index + 22, 2)) != 16) throw new IOException("Parakeet requires mono PCM16 WAV audio.");
                var requestedRate = BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(index + 12, 4));
                if (requestedRate is < 8000 or > 96000 || BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(index + 16, 4)) != requestedRate * 2) throw new IOException("The WAV sample rate is invalid.");
                rate = (int)requestedRate;
            }
            else if (Tag(wav, index, "data"))
            {
                if (dataOffset >= 0 || rate == 0 || length == 0 || (length & 1) != 0) throw new IOException("The WAV PCM data is invalid.");
                dataOffset = index + 8; dataBytes = checked((int)length);
            }
            cursor = next;
        }
        if (rate == 0 || dataOffset < 0 || dataBytes > (long)rate * 2 * 128) throw new IOException("The PCM recording exceeds the supported duration.");
        var result = new List<SpeechChunk>(); var offset = dataOffset; var remaining = dataBytes;
        var maximum = rate * 2 * 25; var minimum = rate * 2 * 18;
        var window = rate / 10 * 2; var hop = Math.Max(2, rate / 50 * 2);
        while (remaining > maximum)
        {
            var cut = maximum; ulong best = ulong.MaxValue;
            for (var start = minimum; start + window <= maximum; start += hop)
            {
                ulong energy = 0;
                for (var i = start; i < start + window; i += 2)
                {
                    var sample = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(offset + i, 2));
                    energy += (ulong)((long)sample * sample);
                }
                if (energy <= best) { best = energy; cut = (start + window / 2) & ~1; }
            }
            result.Add(new(rate, offset, cut)); offset += cut; remaining -= cut;
        }
        result.Add(new(rate, offset, remaining)); return result;
    }
    public static byte[] Build(byte[] wav, SpeechChunk part)
    {
        if (part.SampleRate is < 8000 or > 96000 || part.Offset < 0 || part.Bytes <= 0 || part.Bytes > part.SampleRate * 2 * 25 ||
            (part.Bytes & 1) != 0 || (long)part.Offset + part.Bytes > wav.Length) throw new IOException("Invalid local speech segment.");
        var result = new byte[44 + part.Bytes];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(result, 0); Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(result, 8); Encoding.ASCII.GetBytes("data").CopyTo(result, 36);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4, 4), result.Length - 8);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(20, 2), 1); BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(22, 2), 1);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(24, 4), part.SampleRate); BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(28, 4), part.SampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(32, 2), 2); BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(34, 2), 16);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(40, 4), part.Bytes); wav.AsSpan(part.Offset, part.Bytes).CopyTo(result.AsSpan(44)); return result;
    }
    private static bool Tag(byte[] source, int offset, string tag) => source.AsSpan(offset, 4).SequenceEqual(Encoding.ASCII.GetBytes(tag));
}
