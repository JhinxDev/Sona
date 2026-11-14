using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App.Windows;

// Invoke only through the early, dedicated-process test dispatch. Never use
// SwitchDesktop: this station is noninteractive and has its OWN clipboard.
// https://learn.microsoft.com/en-us/windows/win32/winstation/about-window-stations-and-desktops
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setprocesswindowstation
internal static class NativeClipboardIsolationChecks
{
    internal sealed record Report(int Checks, bool IsolationEstablished, bool? OriginalClipboardUnchanged, bool OriginalClipboardSequenceAvailable, uint CaptureSequenceBefore, uint CaptureSequenceAfter, string[] Failures);
    internal static Report Run()
    {
        int checks = 0; bool isolated = false, originalSequenceAvailable = false;
        bool? unchanged = null;
        uint captureBefore = 0, captureAfter = 0;
        List<string> failures = [];
        var originalStation = GetProcessWindowStation();
        var originalDesktop = GetThreadDesktop(GetCurrentThreadId());
        var originalName = ObjectName(originalStation);
        var originalDesktopName = ObjectName(originalDesktop);
        var originalSequence = GetClipboardSequenceNumber(); // Metadata only, no open/read.
        nint station = 0, desktop = 0, security = 0;
        string stationName = "";
        var desktopName = "Private_" + Guid.NewGuid().ToString("N");
        try
        {
            if (originalStation == 0 || originalName.Length == 0)
                throw new InvalidOperationException("Original station identity is unavailable; isolation refused.");
            if (originalDesktop == 0 || originalDesktopName.Length == 0)
                throw new InvalidOperationException("Original desktop identity is unavailable; isolation refused.");
            // A service station may deny WINSTA_ACCESSCLIPBOARD, for which the
            // sequence API returns zero. That prevents a preservation claim,
            // but does not weaken the independently verified NEW station below.
            // Never open or read the original clipboard to obtain extra proof.
            // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value ?? throw new InvalidOperationException("Current-user identity unavailable.");
            bool administrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;GA;;;" + sid + ")", 1, out security, out _))
                throw NativeFailure("Private station security descriptor");
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = security };
            // A system-assigned name does not require administrator membership.
            // CREATE_ONLY still refuses an existing station, so this can never
            // attach to an existing service/user clipboard. Verify its returned
            // identity and noninteractive flag before selecting it.
            // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowstationw
            // Hosted Windows CI runs as an administrator and can create a GUID
            // name. Unprivileged hosts use only the documented unnamed route;
            // both use CREATE_ONLY and neither may attach to an existing station.
            var requestedName = administrator ? "SonaClipboardCheck_" + Guid.NewGuid().ToString("N") : null;
            station = CreateWindowStation(requestedName, 1, 0x37F, ref attributes);
            if (station == 0) throw NativeFailure("Create private window station");
            stationName = ObjectName(station);
            if (stationName.Length == 0 || stationName == originalName || requestedName != null && stationName != requestedName || IsVisible(station))
                throw new InvalidOperationException("New station identity is not isolated.");
            if (!SetProcessWindowStation(station)) throw NativeFailure("Set private window station");
            if (ObjectName(GetProcessWindowStation()) != stationName || stationName == originalName || IsVisible(station))
                throw new InvalidOperationException("Private noninteractive station verification failed; no clipboard writes allowed.");
            desktop = CreateDesktop(desktopName, null, 0, 0, 0x1FF, ref attributes);
            if (desktop == 0 || !SetThreadDesktop(desktop)) throw NativeFailure("Create and select private desktop");
            if (ObjectName(GetThreadDesktop(GetCurrentThreadId())) != desktopName)
                throw new InvalidOperationException("Private desktop verification failed.");
            Exception? workerFailure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    if (!SetThreadDesktop(desktop) || ObjectName(GetThreadDesktop(GetCurrentThreadId())) != desktopName ||
                        ObjectName(GetProcessWindowStation()) != stationName || IsVisible(station))
                        throw new InvalidOperationException("STA isolation verification failed before WPF initialization.");
                    isolated = true;
                    RunClipboardCases(stationName, desktopName, ref checks, ref captureBefore, ref captureAfter);
                }
                catch (Exception error) { workerFailure = error; }
            }) { IsBackground = true, Name = "Sona isolated clipboard regression" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            // A hung OLE renderer must never survive station restoration. Exit
            // this test process while still isolated instead of moving it back.
            if (!worker.Join(TimeSpan.FromSeconds(30))) Environment.FailFast("Isolated clipboard test timed out; original station was never restored.");
            if (workerFailure != null) throw new InvalidOperationException("Private clipboard case: " + workerFailure.Message, workerFailure);
        }
        catch (Exception error) { failures.Add(error.Message); }
        finally
        {
            // The STA has terminated and disposed its hidden windows before the
            // process reconnects. No clipboard mutation follows reconnection.
            if (originalStation != 0 && !SetProcessWindowStation(originalStation)) failures.Add("Original station restoration failed.");
            if (originalDesktop != 0 && !SetThreadDesktop(originalDesktop)) failures.Add("Original desktop restoration failed.");
            if (ObjectName(GetProcessWindowStation()) == originalName && ObjectName(GetThreadDesktop(GetCurrentThreadId())) == originalDesktopName)
            {
                var finalOriginalSequence = GetClipboardSequenceNumber();
                originalSequenceAvailable = originalSequence != 0 && finalOriginalSequence != 0;
                if (originalSequenceAvailable)
                {
                    unchanged = finalOriginalSequence == originalSequence;
                    if (unchanged == false) failures.Add("Original clipboard sequence changed during the isolated run; concurrent user copy may also cause this.");
                }
            }
            if (desktop != 0 && !CloseDesktop(desktop)) failures.Add("Private desktop cleanup failed.");
            if (station != 0 && !CloseWindowStation(station)) failures.Add("Private window station cleanup failed.");
            if (security != 0) LocalFree(security);
        }
        return new(checks, isolated, unchanged, originalSequenceAvailable, captureBefore, captureAfter, failures.ToArray());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunClipboardCases(string station, string desktop, ref int checks, ref uint captureBefore, ref uint captureAfter)
    {
        AssertIsolated(station, desktop);
        try
        {
            using var window = new HwndSource(new HwndSourceParameters("Sona private clipboard owner") { WindowStyle = 0, Width = 1, Height = 1 });
            var clipboard = new OwnedClipboard(() => window.Handle);
            var native = new ClipboardNative();
            void Check(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }

            // Exactly the WPF path used by Copy draft, containing OLE bookkeeping.
            AssertIsolated(station, desktop);
            System.Windows.Clipboard.SetText("Sona synthetic original \u00e9\u6c34", TextDataFormat.UnicodeText);
            captureBefore = clipboard.Sequence;
            OwnedClipboard.Snapshot textSnapshot;
            try { textSnapshot = clipboard.Capture(); }
            finally { captureAfter = clipboard.Sequence; }
            using (var snapshot = textSnapshot)
            {
                checks++;
                var staged = clipboard.TrySetText(snapshot, "Sona synthetic temporary draft");
                Check(staged.Status == ClipboardChangeStatus.Staged, "Native WPF text staging failed: " + staged.Status); checks++;
                Check(System.Windows.Clipboard.GetText() == "Sona synthetic temporary draft", "Native staged Unicode readback differs."); checks++;
                var restored = clipboard.Restore(snapshot, staged.Sequence);
                Check(restored.Status == ClipboardChangeStatus.Restored, "Native WPF text restoration failed: " + restored.Status); checks++;
                Check(System.Windows.Clipboard.GetText() == "Sona synthetic original \u00e9\u6c34", "Native restored Unicode readback differs."); checks++;
            }

            var rich = new System.Windows.DataObject();
            rich.SetData(DataFormats.UnicodeText, "Sona rich synthetic", false);
            rich.SetData(DataFormats.Html, "<html><body><b>Sona rich synthetic</b></body></html>", false);
            AssertIsolated(station, desktop);
            System.Windows.Clipboard.SetDataObject(rich, true);
            using (var snapshot = clipboard.Capture())
            {
                var original = snapshot.Formats.Select(f => (f.Format, Bytes: f.Bytes.ToArray())).ToArray();
                Check(original.Any(f => f.Format >= 0xC000 && native.FormatName(f.Format) == "HTML Format"), "WPF rich copy did not offer HTML."); checks++;
                var staged = clipboard.TrySetText(snapshot, "temporary rich replacement");
                Check(staged.Status == ClipboardChangeStatus.Staged, "Native rich staging failed."); checks++;
                Check(clipboard.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored, "Native rich restoration failed."); checks++;
                using var restored = clipboard.Capture();
                Check(original.Length == restored.Formats.Count && original.Zip(restored.Formats).All(pair => pair.First.Format == pair.Second.Format && pair.First.Bytes.SequenceEqual(pair.Second.Bytes)), "Native rich formats/order/bytes changed."); checks++;
            }
            AssertIsolated(station, desktop);
            byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 255, 70, 80, 90, 255, 100, 110, 120, 255];
            var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
            image.Freeze();
            System.Windows.Clipboard.SetImage(image);
            using (var snapshot = clipboard.Capture())
            {
                Check(snapshot.Formats.Any(f => f.Format is 2 or 8 or 17), "WPF image did not offer a supported image payload."); checks++;
                var staged = clipboard.TrySetText(snapshot, "temporary image replacement");
                Check(staged.Status == ClipboardChangeStatus.Staged, "Native bitmap staging failed."); checks++;
                Check(clipboard.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored, "Native bitmap restoration failed."); checks++;
                var restored = System.Windows.Clipboard.GetImage() ?? throw new InvalidOperationException("Restored image unavailable.");
                var converted = new FormatConvertedBitmap(restored, PixelFormats.Bgra32, null, 0);
                byte[] restoredPixels = new byte[pixels.Length];
                converted.CopyPixels(restoredPixels, 8, 0);
                Check(restored.PixelWidth == 2 && restored.PixelHeight == 2 && restoredPixels.SequenceEqual(pixels), "Native image pixel readback differs."); checks++;
            }
            foreach (bool unavailableReadback in new[] { false, true })
            {
                AssertIsolated(station, desktop);
                System.Windows.Clipboard.SetText("Synthetic copy before completed paste");
                var target = new DraftTarget(window.Handle, Environment.ProcessId, [10, 20], 0);
                using var insertion = new Insertion(() => target, () => false, _ => throw new InvalidOperationException("Native keys are forbidden in this fixture."));
                var transport = new NativeClipboardFakeInput(clipboard, () => AssertIsolated(station, desktop)) { UnavailableReadback = unavailableReadback };
                insertion.PasteTransportForTests = transport;
                var result = insertion.PasteAsync(target, "Synthetic full completed draft", true).GetAwaiter().GetResult();
                Check(result.Inserted && transport.Pastes == 1 && transport.Field == "Synthetic full completed draft", "Full PasteAsync failed with real WPF/Win32 clipboard: " + insertion.LastClipboardStatus); checks++;
                Check(transport.Enters == (unavailableReadback ? 0 : 1) && insertion.LastPasteConfirmed == !unavailableReadback, "Full PasteAsync send/readback guard differs."); checks++;
                Check(insertion.LastClipboardStatus == "Restored" && !insertion.LastDraftOnClipboard && System.Windows.Clipboard.GetText() == "Synthetic copy before completed paste", "Full PasteAsync did not restore the original WPF copy."); checks++;
            }
            AssertIsolated(station, desktop);
            System.Windows.Clipboard.SetText("Synthetic copy before ownership race");
            {
                var target = new DraftTarget(window.Handle, Environment.ProcessId, [30], 0);
                using var insertion = new Insertion(() => target, () => false, _ => throw new InvalidOperationException("Native keys are forbidden in this fixture."));
                var transport = new NativeClipboardFakeInput(clipboard, () => AssertIsolated(station, desktop))
                { AfterPaste = () => System.Windows.Clipboard.SetText("Newer synthetic owner copy") };
                insertion.PasteTransportForTests = transport;
                insertion.PasteAsync(target, "Synthetic race draft", true).GetAwaiter().GetResult();
                Check(transport.Pastes == 1 && transport.Enters == 0 && System.Windows.Clipboard.GetText() == "Newer synthetic owner copy", "Full PasteAsync overwrote a newer copy or submitted across clipboard change."); checks++;
            }
            AssertIsolated(station, desktop);
            System.Windows.Clipboard.Clear();
        }
        finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
    }

    // Real clipboard transaction, fake target/readback and key gestures. No
    // SendInput, UI Automation, microphone, or foreground changes are involved.
    private sealed class NativeClipboardFakeInput(OwnedClipboard clipboard, Action assertIsolated) : ICompletedDraftTransport
    {
        internal int Pastes, Enters;
        internal string Field = "";
        internal bool UnavailableReadback;
        internal Action? AfterPaste;
        public object SaveClipboard() { assertIsolated(); return clipboard.Capture(); }
        public uint SavedClipboardSequence(object? previous) => ((OwnedClipboard.Snapshot)previous!).Sequence;
        public uint SavedClipboardInitialSequence(object? previous) => ((OwnedClipboard.Snapshot)previous!).InitialSequence;
        public ClipboardChange SetClipboard(object? previous, string text) { assertIsolated(); return clipboard.TrySetText((OwnedClipboard.Snapshot)previous!, text); }
        public uint ClipboardSequence => clipboard.Sequence;
        public ClipboardChange RestoreClipboard(object? previous, uint expectedSequence) { assertIsolated(); return clipboard.Restore((OwnedClipboard.Snapshot)previous!, expectedSequence); }
        public bool Paste() { assertIsolated(); Pastes++; Field += System.Windows.Clipboard.GetText(); AfterPaste?.Invoke(); return true; }
        public bool Enter() { Enters++; return true; }
        public bool EnterHeld => false;
        public string? ReadText(DraftTarget target) => UnavailableReadback ? null : Field;
        public Task Delay(int milliseconds) => Task.CompletedTask;
    }

    private static void AssertIsolated(string station, string desktop)
    {
        if (ObjectName(GetProcessWindowStation()) != station || ObjectName(GetThreadDesktop(GetCurrentThreadId())) != desktop || IsVisible(GetProcessWindowStation()))
            throw new InvalidOperationException("Clipboard write refused outside the verified private station/desktop.");
    }
    private static string ObjectName(nint handle)
    {
        var name = new StringBuilder(256);
        if (handle == 0 || !GetUserObjectInformation(handle, 2, name, name.Capacity * 2, out _)) return "";
        return name.ToString();
    }
    private static bool IsVisible(nint station)
    {
        if (!GetUserObjectFlags(station, 1, out var flags, Marshal.SizeOf<UserObjectFlags>(), out _)) throw NativeFailure("Read private station flags");
        return (flags.Flags & 1) != 0;
    }
    private static Exception NativeFailure(string operation)
    {
        int error = Marshal.GetLastPInvokeError();
        return new Win32Exception(error, operation + " failed (Win32 " + error + ").");
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public nint Descriptor; public int Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct UserObjectFlags { public int Inherit, Reserved; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint GetProcessWindowStation();
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowStation(string? name, uint flags, uint access, ref SecurityAttributes attributes);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetProcessWindowStation(nint station);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateDesktop(string name, string? device, nint mode, uint flags, uint access, ref SecurityAttributes attributes);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetThreadDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder value, int bytes, out int needed);
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetUserObjectFlags(nint handle, int index, out UserObjectFlags value, int bytes, out int needed);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseWindowStation(nint station);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text, uint revision, out nint descriptor, out uint bytes);
    [DllImport("kernel32.dll")] private static extern nint LocalFree(nint memory);
}
