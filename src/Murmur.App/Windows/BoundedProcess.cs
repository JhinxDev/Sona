using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Murmur.App.Windows;

public sealed class BoundedProcess : IDisposable
{
    private nint job;
    private bool disposed;
    private static nint applicationJob;
    public static void ContainApplication()
    {
        if (applicationJob != 0) return;
        var ownerJob = CreateJobObject(0, null);
        var limits = new ExtendedLimit { Basic = new BasicLimit { Flags = 0x2000 | 0x100 }, ProcessMemory = (nuint)(3072L * 1024 * 1024) };
        if (ownerJob == 0 || !SetInformationJobObject(ownerJob, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimit>()) || !AssignProcessToJobObject(ownerJob, GetCurrentProcess()))
        {
            if (ownerJob != 0) CloseHandle(ownerJob);
            throw new IOException("Windows worker containment is unavailable. Sona cannot start safely.");
        }
        // Deliberately retained until OS process exit. CreateProcess children inherit this
        // non-breakaway job atomically, before any per-operation job assignment.
        applicationJob = ownerJob;
    }
    public static bool InApplicationJob(nint process) => applicationJob != 0 && IsProcessInJob(process, applicationJob, out var member) && member;
    public Process Process { get; }
    public BoundedProcess(ProcessStartInfo info, int memoryMiB = 1536)
    {
        if (memoryMiB is not (1536 or 3072)) throw new ArgumentOutOfRangeException(nameof(memoryMiB));
        if (applicationJob == 0) throw new IOException("Application worker containment was not initialized.");
        job = CreateJobObject(nint.Zero, null);
        if (job == 0) throw new System.ComponentModel.Win32Exception();
        var limits = new ExtendedLimit { Basic = new BasicLimit { Flags = 0x2000 | 0x100 }, ProcessMemory = (nuint)((long)memoryMiB * 1024 * 1024) };
        if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimit>())) { CloseHandle(job); throw new System.ComponentModel.Win32Exception(); }
        info.CreateNoWindow = true; info.UseShellExecute = false; info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        Process = new Process { StartInfo = info };
        try
        {
            if (!Process.Start()) throw new IOException("Transcription worker did not start.");
            if (!InApplicationJob(Process.Handle)) throw new IOException("Worker did not inherit the application job.");
            if (!AssignProcessToJobObject(job, Process.Handle) && !Process.HasExited) throw new System.ComponentModel.Win32Exception();
        }
        catch { try { if (!Process.HasExited) Process.Kill(true); } catch (InvalidOperationException) { } finally { Process.Dispose(); CloseHandle(job); job = 0; } throw; }
    }
    public void Dispose()
    {
        if (disposed) return;
        if (job != 0) { CloseHandle(job); job = 0; }
        if (!Process.HasExited && !Process.WaitForExit(3000)) throw new IOException("Windows has not confirmed worker termination.");
        Process.Dispose(); disposed = true;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimit { public long PerProcess, PerJob; public uint Flags; public nuint MinWorking, MaxWorking; public uint Active; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimit { public BasicLimit Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcess, PeakJob; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(nint job, int kind, ref ExtendedLimit info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool IsProcessInJob(nint process, nint job, out bool member);
}
