namespace Sona.Core;

public static class RecordingLimits
{
    public const int MaximumSeconds = 300;
    public const int PcmBytesPerSecond = 32000;
    public const int MaximumPcmBytes = MaximumSeconds * PcmBytesPerSecond;
}
