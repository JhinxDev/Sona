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
    internal sealed record StageObservation(uint ReturnedSequence, uint AfterCloseSequence, uint AfterReadSequence, bool OwnerMatchesAfterClose, bool OwnerMatchesAfterRead);
    internal sealed record BitmapObservation(bool HandlePresent, int GetObjectBytes, int Width, int Height, int Stride, int Planes, int BitsPerPixel, int Error);
    internal sealed record ImageObservation(string[] OfferedFormats, bool UnsupportedPayloadRefused, bool OriginalImagePreserved, BitmapObservation? SynthesizedBitmap = null, BitmapObservation? ExplicitBitmap = null);
    internal sealed record Report(int Checks, bool IsolationEstablished, bool? OriginalClipboardUnchanged, bool OriginalClipboardSequenceAvailable, uint CaptureSequenceBefore, uint CaptureSequenceAfter, string[] Failures, string Boundary = "PrivateWindowStation", StageObservation? Stage = null, ImageObservation? Image = null);

    // This explicit entrypoint runs ONLY on a disposable GitHub-hosted VM.
    // Environment checks prevent accidental local/self-hosted use; they are not
    // a security boundary against a caller deliberately manufacturing values.
    // Never invoke this as a fallback after private-station isolation fails.
    internal static Report RunOnHostedRunner()
    {
        int checks = 0; bool boundaryVerified = false;
        StageObservation? stage = null;
        ImageObservation? image = null;
        uint captureBefore = 0, captureAfter = 0;
        List<string> failures = [];
        try
        {
            RequireHostedRunner(); // Before WPF, clipboard, or desktop APIs.
            var stationName = ObjectName(GetProcessWindowStation());
            var desktopName = ObjectName(GetThreadDesktop(GetCurrentThreadId()));
            if (stationName.Length == 0 || desktopName.Length == 0)
                throw new InvalidOperationException("Hosted runner station/desktop identity is unavailable.");
            void VerifyBoundary()
            {
                RequireHostedRunner();
                if (ObjectName(GetProcessWindowStation()) != stationName || ObjectName(GetThreadDesktop(GetCurrentThreadId())) != desktopName)
                    throw new InvalidOperationException("Hosted runner station/desktop identity changed.");
            }
            Exception? workerFailure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    VerifyBoundary();
                    boundaryVerified = true;
                    RunClipboardCases(VerifyBoundary, ref checks, ref captureBefore, ref captureAfter, ref stage, ref image);
                }
                catch (Exception error) { workerFailure = error; }
            }) { IsBackground = true, Name = "Sona hosted native clipboard regression" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            if (!worker.Join(TimeSpan.FromSeconds(30))) Environment.FailFast("Hosted native clipboard test timed out.");
            if (workerFailure != null) failures.Add("Hosted clipboard case: " + workerFailure.Message);
        }
        catch (Exception error) { failures.Add(error.Message); }
        // This mode intentionally replaces the disposable VM's clipboard. It
        // makes no private-window-station or original-clipboard preservation claim.
        return new(checks, boundaryVerified, null, false, captureBefore, captureAfter, failures.ToArray(), "DisposableGitHubHostedRunner", stage, image);
    }

    private static void RequireHostedRunner()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||
            Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") != "JhinxDev/Sona")
            throw new InvalidOperationException("Hosted clipboard test requires the JhinxDev/Sona GitHub-hosted workflow; local and self-hosted execution is refused.");
    }
    internal static Report Run()
    {
        int checks = 0; bool isolated = false, originalSequenceAvailable = false;
        StageObservation? stage = null;
        ImageObservation? image = null;
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
                    RunClipboardCases(() => AssertIsolated(stationName, desktopName), ref checks, ref captureBefore, ref captureAfter, ref stage, ref image);
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
        return new(checks, isolated, unchanged, originalSequenceAvailable, captureBefore, captureAfter, failures.ToArray(), Stage: stage, Image: image);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunClipboardCases(Action assertBoundary, ref int checks, ref uint captureBefore, ref uint captureAfter, ref StageObservation? observation, ref ImageObservation? imageObservation)
    {
        assertBoundary();
        try
        {
            using var window = new HwndSource(new HwndSourceParameters("Sona private clipboard owner") { WindowStyle = 0, Width = 1, Height = 1 });
            var clipboard = new OwnedClipboard(() => window.Handle);
            var native = new ClipboardNative();
            void Check(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }

            // Exactly the WPF path used by Copy draft, containing OLE bookkeeping.
            assertBoundary();
            System.Windows.Clipboard.SetText("Sona synthetic original \u00e9\u6c34", TextDataFormat.UnicodeText);
            captureBefore = clipboard.Sequence;
            OwnedClipboard.Snapshot textSnapshot;
            try { textSnapshot = clipboard.Capture(); }
            finally { captureAfter = clipboard.Sequence; }
            using (var snapshot = textSnapshot)
            {
                checks++;
                var staged = clipboard.TrySetText(snapshot, "Sona synthetic temporary draft \u00e9\u6c34");
                observation = new(staged.Sequence, clipboard.Sequence, 0, GetClipboardOwner() == window.Handle, false);
                Check(staged.Status == ClipboardChangeStatus.Staged, "Native WPF text staging failed: " + staged.Status); checks++;
                Check(System.Windows.Clipboard.GetText() == "Sona synthetic temporary draft \u00e9\u6c34", "Native staged Unicode readback differs."); checks++;
                observation = observation with { AfterReadSequence = clipboard.Sequence, OwnerMatchesAfterRead = GetClipboardOwner() == window.Handle };
                var restored = clipboard.Restore(snapshot, staged.Sequence);
                Check(restored.Status == ClipboardChangeStatus.Restored, "Native WPF text restoration failed: " + restored.Status); checks++;
                Check(System.Windows.Clipboard.GetText() == "Sona synthetic original \u00e9\u6c34", "Native restored Unicode readback differs."); checks++;
            }

            var rich = new System.Windows.DataObject();
            rich.SetData(DataFormats.UnicodeText, "Sona rich synthetic", false);
            rich.SetData(DataFormats.Html, "<html><body><b>Sona rich synthetic</b></body></html>", false);
            assertBoundary();
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
            assertBoundary();
            byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 255, 70, 80, 90, 255, 100, 110, 120, 255];
            var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
            image.Freeze();
            System.Windows.Clipboard.SetImage(image);
            // WPF can additionally offer its serialized framework bitmap object.
            // Record only the synthetic format names, not clipboard payloads.
            // Unknown real payloads must remain a safe refusal, never be ignored.
            if (!native.Open(window.Handle)) throw new ExternalException("Synthetic WPF image enumeration is unavailable.");
            (uint Format, string Name)[] imageFormats;
            try { imageFormats = native.Formats(128).Select(format => (format, native.FormatName(format))).ToArray(); }
            finally { native.Close(); }
            imageObservation = new(imageFormats.Select(entry => entry.Format + ":" + entry.Name).ToArray(), false, false);
            var imageSequence = clipboard.Sequence;
            bool imageRefused = false;
            try { using var supportedImage = clipboard.Capture(); }
            catch (ExternalException error) when (error.Message == "Clipboard contains a format that cannot be safely preserved.") { imageRefused = true; }
            if (imageRefused)
            {
                Check(imageFormats.Any(entry => entry.Format >= 0xC000 && entry.Name is not ("DataObject" or "Ole Private Data" or "PNG")), "WPF image refusal must correspond to an additional real registered payload."); checks++;
                bool preserved = clipboard.Sequence == imageSequence && ImagePixelsMatch(pixels);
                imageObservation = imageObservation with { UnsupportedPayloadRefused = true, OriginalImagePreserved = preserved };
                Check(preserved, "Refusing an unknown WPF image payload changed the original image."); checks++;
            }
            else
            {
                imageObservation = imageObservation with { OriginalImagePreserved = ImagePixelsMatch(pixels) };
                Check(imageObservation.OriginalImagePreserved, "Supported WPF image capture changed its pixels."); checks++;
            }

            // Separately qualify supported native images without a serialized
            // framework object: 40-byte BITMAPINFOHEADER + a top-down 32-bit DIB.
            // Windows supplies its documented CF_BITMAP/CF_DIBV5 conversions.
            // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader
            byte[] dib = new byte[40 + pixels.Length];
            BitConverter.GetBytes(40).CopyTo(dib, 0);
            BitConverter.GetBytes(2).CopyTo(dib, 4);
            BitConverter.GetBytes(-2).CopyTo(dib, 8);
            BitConverter.GetBytes((ushort)1).CopyTo(dib, 12);
            BitConverter.GetBytes((ushort)32).CopyTo(dib, 14);
            BitConverter.GetBytes(pixels.Length).CopyTo(dib, 20);
            pixels.CopyTo(dib, 40);
            assertBoundary();
            nint dibHandle = native.Allocate(dib);
            if (dibHandle == 0) throw new ExternalException("Synthetic DIB allocation failed.");
            try
            {
                if (!native.Open(window.Handle)) throw new ExternalException("Synthetic DIB seed lock failed.");
                try
                {
                    if (!native.Empty() || !native.Set(8, dibHandle)) throw new ExternalException("Synthetic DIB seed failed.");
                    dibHandle = 0; // Windows now owns the allocation.
                }
                finally { native.Close(); }
            }
            finally { if (dibHandle != 0) native.Free(8, dibHandle); }
            imageObservation = imageObservation with { SynthesizedBitmap = ObserveBitmap(native, window.Handle) };

            // Do not assume a service-session display can synthesize a usable
            // HBITMAP from a DIB. Record that capability separately, then qualify
            // an explicit, real CF_BITMAP source alongside the exact DIB bytes.
            // This does not broaden production acceptance of unreadable formats.
            assertBoundary();
            dibHandle = native.Allocate(dib);
            nint bitmapHandle = CreateDIBSection(0, dib, 0, out var bitmapPixels, 0, 0);
            try
            {
                if (dibHandle == 0 || bitmapHandle == 0 || bitmapPixels == 0) throw new ExternalException("Explicit native image allocation failed.");
                Marshal.Copy(pixels, 0, bitmapPixels, pixels.Length);
                if (!native.Open(window.Handle)) throw new ExternalException("Explicit native image seed lock failed.");
                try
                {
                    if (!native.Empty() || !native.Set(8, dibHandle)) throw new ExternalException("Explicit DIB seed failed.");
                    dibHandle = 0;
                    if (!native.Set(2, bitmapHandle)) throw new ExternalException("Explicit bitmap seed failed.");
                    bitmapHandle = 0;
                }
                finally { native.Close(); }
            }
            finally
            {
                if (dibHandle != 0) native.Free(8, dibHandle);
                if (bitmapHandle != 0) native.Free(2, bitmapHandle);
            }
            imageObservation = imageObservation with { ExplicitBitmap = ObserveBitmap(native, window.Handle) };
            Check(imageObservation.ExplicitBitmap is { HandlePresent: true, Width: 2, Height: 2, Planes: 1, BitsPerPixel: 32 }, "Explicit native clipboard bitmap is not a valid 2x2 image."); checks++;
            using (var snapshot = clipboard.Capture())
            {
                Check(snapshot.Formats.Any(f => f.Format == 8) && snapshot.Formats.Any(f => f.Format == 2), "Native DIB seed did not offer DIB and bitmap payloads."); checks++;
                var staged = clipboard.TrySetText(snapshot, "temporary image replacement");
                Check(staged.Status == ClipboardChangeStatus.Staged, "Native bitmap staging failed."); checks++;
                Check(clipboard.Restore(snapshot, staged.Sequence).Status == ClipboardChangeStatus.Restored, "Native bitmap restoration failed."); checks++;
                Check(ImagePixelsMatch(pixels), "Native image pixel readback differs."); checks++;
            }
            foreach (bool unavailableReadback in new[] { false, true })
            {
                assertBoundary();
                System.Windows.Clipboard.SetText("Synthetic copy before completed paste");
                var target = new DraftTarget(window.Handle, Environment.ProcessId, [10, 20], 0);
                using var insertion = new Insertion(() => target, () => false, _ => throw new InvalidOperationException("Native keys are forbidden in this fixture."));
                var transport = new NativeClipboardFakeInput(clipboard, assertBoundary) { UnavailableReadback = unavailableReadback };
                insertion.PasteTransportForTests = transport;
                var result = insertion.PasteAsync(target, "Synthetic full completed draft", true).GetAwaiter().GetResult();
                Check(result.Inserted && transport.Pastes == 1 && transport.Field == "Synthetic full completed draft", "Full PasteAsync failed with real WPF/Win32 clipboard: " + insertion.LastClipboardStatus); checks++;
                Check(transport.Enters == (unavailableReadback ? 0 : 1) && insertion.LastPasteConfirmed == !unavailableReadback, "Full PasteAsync send/readback guard differs."); checks++;
                Check(insertion.LastClipboardStatus == "Restored" && !insertion.LastDraftOnClipboard && System.Windows.Clipboard.GetText() == "Synthetic copy before completed paste", "Full PasteAsync did not restore the original WPF copy."); checks++;
            }
            assertBoundary();
            System.Windows.Clipboard.SetText("Synthetic copy before ownership race");
            {
                var target = new DraftTarget(window.Handle, Environment.ProcessId, [30], 0);
                using var insertion = new Insertion(() => target, () => false, _ => throw new InvalidOperationException("Native keys are forbidden in this fixture."));
                var transport = new NativeClipboardFakeInput(clipboard, assertBoundary)
                { AfterPaste = () => System.Windows.Clipboard.SetText("Newer synthetic owner copy") };
                insertion.PasteTransportForTests = transport;
                insertion.PasteAsync(target, "Synthetic race draft", true).GetAwaiter().GetResult();
                Check(transport.Pastes == 1 && transport.Enters == 0 && System.Windows.Clipboard.GetText() == "Newer synthetic owner copy", "Full PasteAsync overwrote a newer copy or submitted across clipboard change."); checks++;
            }
        }
        finally
        {
            // Clear only inside the verified test boundary, including when a
            // test assertion fails. Never clear after a boundary verification failure.
            try { assertBoundary(); System.Windows.Clipboard.Clear(); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }
    }

    private static bool ImagePixelsMatch(byte[] expected)
    {
        var restored = System.Windows.Clipboard.GetImage();
        if (restored == null || restored.PixelWidth != 2 || restored.PixelHeight != 2) return false;
        // BI_RGB's high byte is reserved, not an alpha channel. Prove every
        // opaque pixel's B/G/R values without inventing an alpha-preservation claim.
        var converted = new FormatConvertedBitmap(restored, PixelFormats.Bgr24, null, 0);
        byte[] pixels = new byte[12];
        converted.CopyPixels(pixels, 6, 0);
        return pixels.SequenceEqual(expected.Where((_, index) => index % 4 != 3));
    }

    private static BitmapObservation ObserveBitmap(ClipboardNative native, nint owner)
    {
        if (!native.Open(owner)) throw new ExternalException("Synthetic bitmap metadata lock failed.");
        try
        {
            Marshal.SetLastPInvokeError(0);
            var handle = GetClipboardData(2);
            if (handle == 0) return new(false, 0, 0, 0, 0, 0, 0, Marshal.GetLastPInvokeError());
            Marshal.SetLastPInvokeError(0);
            var size = GetObject(handle, Marshal.SizeOf<NativeBitmap>(), out var bitmap);
            return new(true, size, bitmap.Width, bitmap.Height, bitmap.Stride, bitmap.Planes, bitmap.BitsPerPixel, Marshal.GetLastPInvokeError());
        }
        finally { native.Close(); }
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
    [DllImport("user32.dll")] private static extern nint GetClipboardOwner();
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetClipboardData(uint format);
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW", SetLastError = true)] private static extern int GetObject(nint handle, int size, out NativeBitmap bitmap);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, byte[] info, uint usage, out nint pixels, nint section, uint offset);
    [StructLayout(LayoutKind.Sequential)] private struct NativeBitmap
    {
        public int Type, Width, Height, Stride;
        public ushort Planes, BitsPerPixel;
        public nint Pixels;
    }
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
