using System.Buffers.Binary;
using Sona.Core;
internal static class SpeechAudioChecks
{
    public static void Run(Action<bool, string> check)
    {
        var pcm = Enumerable.Range(0, 16000 * 2 * 81).Select(i => (byte)(i % 251)).ToArray();
        var wav = SpeechAudio.Build(pcm, new(96000, 0, pcm.Length));
        // Rewrite the synthesis header to an actual 81-second 16 kHz fixture.
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24, 4), 16000); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28, 4), 32000);
        var parts = SpeechAudio.Plan(wav);
        check(parts.Count > 3 && parts.All(p => p.Bytes <= 25 * 32000 && (p.Bytes & 1) == 0), "Long PCM is segmented into bounded aligned requests");
        var reconstructed = parts.SelectMany(p => SpeechAudio.Build(wav, p).Skip(44)).ToArray();
        check(reconstructed.SequenceEqual(pcm) && parts[0].Offset == 44 && parts[^1].Offset + parts[^1].Bytes == wav.Length, "Every sample including the final remainder survives segmentation exactly once");
        var quiet = (byte[])wav.Clone(); Array.Clear(quiet, 44 + 21 * 32000, 3200); var quietParts = SpeechAudio.Plan(quiet);
        check(quietParts[0].Bytes >= 21 * 32000 && quietParts[0].Bytes <= 21 * 32000 + 3200, "Segment boundaries prefer a quiet PCM window before the duration limit");
        var shortWave = SpeechAudio.Build(pcm, new(16000, 0, 16000));
        check(SpeechAudio.Plan(shortWave).Count == 1, "Short dictation stays one full-utterance request");
        bool Rejected(byte[] value) { try { SpeechAudio.Plan(value); return false; } catch (IOException) { return true; } }
        var incomplete = wav[..^2]; check(Rejected(incomplete) && Rejected(new byte[44]), "Truncated and malformed WAV audio fail before any partial transcription");
        var stereo = (byte[])wav.Clone(); BinaryPrimitives.WriteInt16LittleEndian(stereo.AsSpan(22, 2), 2);
        var badRate = (byte[])wav.Clone(); BinaryPrimitives.WriteUInt32LittleEndian(badRate.AsSpan(24, 4), uint.MaxValue);
        check(Rejected(stereo) && Rejected(badRate), "Unsupported PCM format and extreme sample rates fail with a bounded error");
        check(SpeechAudio.Plan(wav).Sum(p => p.Bytes) == pcm.Length && wav.AsSpan(44).SequenceEqual(pcm), "Planning never modifies original audio");
        var rebuilt = SpeechAudio.Build(wav, parts[^1]); check(SpeechAudio.Plan(rebuilt).Single().Bytes == parts[^1].Bytes, "Final remainder carries a valid independent WAV header");
        Array.Clear(pcm); Array.Clear(wav); Array.Clear(quiet); Array.Clear(reconstructed);
    }
}
