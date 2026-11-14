using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Sona.App.Windows;

internal enum ClipboardChangeStatus { Staged, Restored, OwnershipChanged, Unavailable, FailedRestored, FailedOwnedPartial }
internal readonly record struct ClipboardChange(ClipboardChangeStatus Status, uint Sequence);

// This boundary deliberately has no retrying OLE/WPF calls. Check the sequence
// AFTER acquiring OpenClipboard and keep that lock through the entire mutation.
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setclipboarddata
internal sealed class OwnedClipboard
{
    internal const int MaximumBytes = 64 * 1024 * 1024;
    internal const int MaximumFormats = 128;
    private readonly IClipboardNative native;
    private readonly Func<nint> owner;
    internal OwnedClipboard(Func<nint> owner, IClipboardNative? native = null)
    { this.owner = owner; this.native = native ?? new ClipboardNative(); }
    internal uint Sequence => native.Sequence;
    internal readonly record struct Content(uint Format, byte[] Bytes, nint Bitmap = 0, int BitmapBytes = 0, bool Marker = false);

    internal sealed class Snapshot : IDisposable
    {
        internal readonly OwnedClipboard Source;
        internal readonly List<Content> Formats = [];
        internal uint Sequence;
        internal uint InitialSequence;
        internal bool Disposed;
        internal Snapshot(OwnedClipboard source) => Source = source;
        public void Dispose()
        {
            if (Disposed) return;
            foreach (var entry in Formats)
            {
                CryptographicOperations.ZeroMemory(entry.Bytes);
                if (entry.Bitmap != 0) Source.native.Free(2, entry.Bitmap);
            }
            Formats.Clear(); Disposed = true;
        }
    }

    internal Snapshot Capture()
    {
        var hwnd = owner();
        if (hwnd == 0 || !native.Open(hwnd)) throw new ExternalException("Clipboard is unavailable.");
        var snapshot = new Snapshot(this);
        try
        {
            // Capture the owner generation before delayed rendering can change
            // the sequence. Staging uses the fully materialized final Sequence.
            snapshot.InitialSequence = native.Sequence;
            var offered = native.Formats(MaximumFormats);
            var formats = offered.Where(format => !IsOleBookkeeping(format)).ToArray();
            if (offered.Length != 0 && formats.Length == 0)
                throw new ExternalException("Clipboard has no materializable content formats.");
            // Preserve every offered payload format or refuse before modifying anything.
            // Unknown registered formats can contain owner-bound OLE structures;
            // private/palette/metafile formats need distinct lifetime semantics.
            // OleDuplicateData does not turn those into bounded, portable bytes.
            // https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats
            foreach (var format in formats)
                if (!Supported(format)) throw new ExternalException("Clipboard contains a format that cannot be safely preserved.");
            var remaining = MaximumBytes;
            foreach (var format in formats)
            {
                if (format == 2)
                {
                    var bitmap = native.ReadBitmap(remaining);
                    snapshot.Formats.Add(new(format, [], bitmap.Handle, bitmap.Bytes));
                    remaining -= bitmap.Bytes;
                    continue;
                }
                var bytes = native.Read(format, remaining);
                snapshot.Formats.Add(new(format, bytes, Marker: bytes.Length == 0)); remaining -= bytes.Length;
            }
            // Delayed renderers may add formats while answering GetClipboardData.
            // Refuse an incomplete snapshot instead of silently dropping them.
            if (!formats.SequenceEqual(native.Formats(MaximumFormats).Where(format => !IsOleBookkeeping(format))))
                throw new ExternalException("Clipboard formats changed during capture.");
            snapshot.Sequence = native.Sequence;
            return snapshot;
        }
        catch { snapshot.Dispose(); throw; }
        finally { native.Close(); }
    }

    // These exact OLE bookkeeping formats describe the original IDataObject's
    // owner/FORMATETC transport, not user payload. Replaying the original HWND
    // or marshalled object descriptor after taking ownership would be invalid.
    // Materialize all supported payloads; unknown real OLE object formats still
    // refuse. OleGetClipboard builds a data object over the restored formats.
    // https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-olegetclipboard
    // https://github.com/wine-mirror/wine/blob/master/dlls/ole32/tests/clipboard.c
    private bool IsOleBookkeeping(uint format) => format >= 0xC000 &&
        native.FormatName(format) is "DataObject" or "Ole Private Data";

    private bool Supported(uint format) => format is 1 or 2 or 4 or 5 or 6 or 7 or 8 or 11 or 13 or 15 or 16 or 17 ||
        format >= 0xC000 && native.FormatName(format) is
            "HTML Format" or "Rich Text Format" or "Rich Text Format Without Objects" or "PNG" or
            "Chromium internal source URL" or "WebKit Smart Paste Format" or
            "Preferred DropEffect" or "Performed DropEffect" or "Paste Succeeded" or
            "ExcludeClipboardContentFromMonitorProcessing" or "CanIncludeInClipboardHistory" or "CanUploadToCloudClipboard";

    internal ClipboardChange TrySetText(Snapshot snapshot, string text)
    {
        Validate(snapshot);
        if (text.Length > MaximumBytes / 2 - 1 || text.Contains('\0'))
            return new(ClipboardChangeStatus.Unavailable, 0);
        byte[] bytes = Encoding.Unicode.GetBytes(text + '\0');
        byte[] ansi = [], oem = [];
        try
        {
            // Windows documents DWORD zero to exclude temporary content from
            // clipboard history and cloud sync. Publish these while still locked.
            // https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats#cloud-clipboard-and-clipboard-history-formats
            var history = native.RegisterFormat("CanIncludeInClipboardHistory");
            var cloud = native.RegisterFormat("CanUploadToCloudClipboard");
            var locale = native.TextLocale;
            if (history == 0 || cloud == 0 || locale == 0) return new(ClipboardChangeStatus.Unavailable, 0);
            // Build all legacy representations BEFORE taking ownership. Windows
            // cannot synthesize them until CloseClipboard, which would advance
            // the sequence after our final locked ownership check.
            ansi = native.EncodeText(text, locale, false, MaximumBytes - bytes.Length - 12);
            oem = native.EncodeText(text, locale, true, MaximumBytes - bytes.Length - ansi.Length - 12);
            using var staging = Prepare([new(history, new byte[4]), new(cloud, new byte[4]), new(16u, BitConverter.GetBytes(locale)), new(13u, bytes), new(1u, ansi), new(7u, oem)]);
            using var rollback = Prepare(snapshot.Formats);
            var hwnd = owner();
            if (hwnd == 0 || !native.Open(hwnd)) return new(ClipboardChangeStatus.Unavailable, 0);
            try
            {
                if (native.Sequence != snapshot.Sequence) return new(ClipboardChangeStatus.OwnershipChanged, native.Sequence);
                if (!native.Empty()) return new(ClipboardChangeStatus.Unavailable, native.Sequence);
                // Never expose draft text if either privacy flag was rejected.
                // CloseClipboard otherwise adds locale/text conversions and
                // advances our sequence after we sampled it. Publish every
                // prebuilt representation while STILL holding the lock.
                // Never adopt a post-close sequence or relax ownership checks.
                // https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats
                // https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats
                if (staging.Publish(stopOnFailure: true)) return new(ClipboardChangeStatus.Staged, native.Sequence);
                // Staging failed after EmptyClipboard. Rollback was allocated
                // BEFORE clearing, and still executes under the same open lock.
                // Remove all partially staged text/privacy formats first.
                var restored = native.Empty() && rollback.Publish();
                return new(restored ? ClipboardChangeStatus.FailedRestored : ClipboardChangeStatus.FailedOwnedPartial, native.Sequence);
            }
            finally { native.Close(); }
        }
        catch (ExternalException) { return new(ClipboardChangeStatus.Unavailable, 0); }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(ansi);
            CryptographicOperations.ZeroMemory(oem);
        }
    }

    // One bounded attempt. FailedOwnedPartial supplies the new owned sequence;
    // callers may retry with that sequence, never the stale pre-mutation value.
    internal ClipboardChange Restore(Snapshot snapshot, uint expectedSequence)
    {
        Validate(snapshot);
        try
        {
            using var prepared = Prepare(snapshot.Formats);
            var hwnd = owner();
            if (hwnd == 0 || !native.Open(hwnd)) return new(ClipboardChangeStatus.Unavailable, expectedSequence);
            try
            {
                if (native.Sequence != expectedSequence) return new(ClipboardChangeStatus.OwnershipChanged, native.Sequence);
                if (!native.Empty()) return new(ClipboardChangeStatus.Unavailable, expectedSequence);
                return new(prepared.Publish() ? ClipboardChangeStatus.Restored : ClipboardChangeStatus.FailedOwnedPartial, native.Sequence);
            }
            finally { native.Close(); }
        }
        catch (ExternalException) { return new(ClipboardChangeStatus.Unavailable, expectedSequence); }
    }

    private void Validate(Snapshot snapshot)
    {
        if (snapshot.Source != this || snapshot.Disposed) throw new InvalidOperationException("Clipboard snapshot is no longer valid.");
    }
    private Prepared Prepare(IEnumerable<Content> formats)
    {
        var result = new Prepared(native);
        try
        {
            foreach (var entry in formats)
            {
                if (entry.Marker) { result.Handles.Add((entry.Format, 0, true)); continue; }
                var handle = entry.Bitmap != 0 ? native.CopyBitmap(entry.Bitmap, entry.BitmapBytes) : native.Allocate(entry.Bytes);
                if (handle == 0) throw new ExternalException("Clipboard allocation failed.");
                result.Handles.Add((entry.Format, handle, false));
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private sealed class Prepared(IClipboardNative native) : IDisposable
    {
        internal readonly List<(uint Format, nint Handle, bool Marker)> Handles = [];
        internal bool Publish(bool stopOnFailure = false)
        {
            // Continue after a failed format so a best-effort rollback restores
            // as much as possible. The complete snapshot remains available.
            bool success = true;
            for (int i = 0; i < Handles.Count; i++)
            {
                var entry = Handles[i];
                if (entry.Marker ? native.SetMarker(entry.Format) : native.Set(entry.Format, entry.Handle)) Handles[i] = (entry.Format, 0, entry.Marker);
                else { success = false; if (stopOnFailure) break; }
            }
            return success;
        }
        public void Dispose()
        {
            foreach (var entry in Handles) if (entry.Handle != 0) native.Free(entry.Format, entry.Handle);
            Handles.Clear();
        }
    }
}

// Native methods return failures, not exceptions, after mutation begins.
// Read/Allocate may throw only before EmptyClipboard. Tests implement this
// boundary in memory, never opening the user's clipboard or sending input.
internal interface IClipboardNative
{
    uint Sequence { get; }
    uint TextLocale { get; }
    byte[] EncodeText(string text, uint locale, bool oem, int maximumBytes);
    bool Open(nint hwnd);
    void Close();
    uint[] Formats(int maximum);
    string FormatName(uint format);
    uint RegisterFormat(string name);
    byte[] Read(uint format, int maximumBytes);
    (nint Handle, int Bytes) ReadBitmap(int maximumBytes);
    nint CopyBitmap(nint handle, int maximumBytes);
    nint Allocate(byte[] bytes);
    void Free(uint format, nint handle);
    bool Empty();
    bool Set(uint format, nint handle);
    bool SetMarker(uint format);
}

internal sealed class ClipboardNative : IClipboardNative
{
    public uint Sequence => GetClipboardSequenceNumber();
    // Match the current input language that Windows would choose on close.
    public uint TextLocale => (uint)(GetKeyboardLayout(0).ToInt64() & 0xffff);
    public byte[] EncodeText(string text, uint locale, bool oem, int maximumBytes)
    {
        // CF_LOCALE determines the Windows ANSI/OEM code pages. Locale values
        // 0 (CP_ACP) / 1 (CP_OEMCP) are documented fallbacks, not failures.
        // LOCALE_RETURN_NUMBER writes a DWORD into a two-WCHAR output buffer.
        // https://learn.microsoft.com/en-us/windows/win32/intl/locale-idefault-constants
        // https://learn.microsoft.com/en-us/windows/win32/api/winnls/nf-winnls-getlocaleinfow
        if (GetLocaleInfo(locale, 0x20000000u | (oem ? 0xBu : 0x1004u), out var codePage, 2) != 2)
            throw new ExternalException("Clipboard locale conversion is unavailable.");
        // -1 includes the terminating NUL; size is BYTES, not UTF-16 chars.
        // Flags/default pointers remain zero so every documented code page,
        // including UTF-8 system locales, uses its supported native conversion.
        // Unicode stays exact; legacy formats use Windows' default substitution.
        // https://learn.microsoft.com/en-us/windows/win32/api/stringapiset/nf-stringapiset-widechartomultibyte
        int count = WideCharToMultiByte(codePage, 0, text, -1, null, 0, 0, 0);
        if (count <= 0 || count > maximumBytes) throw new ExternalException("Clipboard text conversion is unavailable or too large.");
        var result = new byte[count];
        try
        {
            if (WideCharToMultiByte(codePage, 0, text, -1, result, result.Length, 0, 0) != count || result[^1] != 0)
                throw new ExternalException("Clipboard text conversion failed.");
            return result;
        }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
    }
    public bool Open(nint hwnd) => OpenClipboard(hwnd);
    public void Close() => CloseClipboard();
    public bool Empty() => EmptyClipboard();
    public bool Set(uint format, nint handle) => SetClipboardData(format, handle) != 0;
    public uint RegisterFormat(string name) => RegisterClipboardFormat(name);
    public bool SetMarker(uint format)
    {
        // Chromium intentionally uses a null handle for this presence-only
        // marker; its owner also does not render any payload for WM_RENDERFORMAT.
        // https://github.com/chromium/chromium/blob/main/ui/base/clipboard/clipboard_win.cc
        // NULL is not the normal success sentinel. Check marker availability
        // while still holding the lock; it was absent after EmptyClipboard.
        if (FormatName(format) != "WebKit Smart Paste Format") return false;
        SetClipboardData(format, 0);
        return IsClipboardFormatAvailable(format);
    }
    public void Free(uint format, nint handle) { if (format == 2) DeleteObject(handle); else GlobalFree(handle); }
    public (nint Handle, int Bytes) ReadBitmap(int maximumBytes)
    {
        var source = GetClipboardData(2);
        var bytes = BitmapSize(source, maximumBytes);
        var copy = CopyBitmap(source, bytes);
        if (copy == 0) throw new ExternalException("Clipboard bitmap could not be preserved.");
        return (copy, bytes);
    }
    public nint CopyBitmap(nint handle, int maximumBytes)
    {
        BitmapSize(handle, maximumBytes);
        // No COPYRETURNORG or COPYDELETEORG: always an independent owned copy.
        // DIB sections avoid device-dependent storage. DeleteObject releases
        // untransferred copies; Windows owns successful SetClipboardData copies.
        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-copyimage
        return CopyImage(handle, 0, 0, 0, 0x2000);
    }
    private static int BitmapSize(nint handle, int maximumBytes)
    {
        if (handle == 0 || GetObject(handle, Marshal.SizeOf<Bitmap>(), out var value) == 0)
            throw new ExternalException("Clipboard bitmap is unsupported.");
        return BoundBitmapSize(value.Width, value.Height, value.WidthBytes, value.Planes, value.BitsPixel, maximumBytes);
    }
    internal static int BoundBitmapSize(int width, int height, int widthBytes, int planes, int bitsPixel, int maximumBytes)
    {
        if (width <= 0 || height <= 0 || widthBytes <= 0 || planes != 1 || bitsPixel is not (1 or 4 or 8 or 16 or 24 or 32))
            throw new ExternalException("Clipboard bitmap is unsupported.");
        // Include worst-case 32-bit pixels, row alignment and color table/header
        // overhead BEFORE CopyImage, rather than trusting an unbounded GDI copy.
        long stride = Math.Max((long)width * 4, widthBytes);
        // Divide before multiplying: hostile dimensions must not overflow Int64.
        if (maximumBytes < 4096 || stride > (maximumBytes - 4096L) / height)
            throw new ExternalException("Clipboard bitmap is too large.");
        return (int)(stride * height + 4096);
    }
    public uint[] Formats(int maximum)
    {
        List<uint> formats = []; uint current = 0;
        while (true)
        {
            Marshal.SetLastPInvokeError(0);
            current = EnumClipboardFormats(current);
            if (current == 0)
            {
                if (Marshal.GetLastPInvokeError() != 0) throw new ExternalException("Clipboard enumeration failed.");
                return formats.ToArray();
            }
            if (formats.Count == maximum) throw new ExternalException("Clipboard has too many formats.");
            formats.Add(current);
        }
    }
    public string FormatName(uint format)
    {
        var name = new StringBuilder(256);
        return GetClipboardFormatName(format, name, name.Capacity) > 0 ? name.ToString() : "";
    }
    public byte[] Read(uint format, int maximumBytes)
    {
        var handle = GetClipboardData(format);
        // A known presence-only marker is different from an unreadable delayed
        // rendering format. Only this documented Chromium marker accepts NULL.
        if (handle == 0 && FormatName(format) == "WebKit Smart Paste Format") return [];
        if (handle == 0) throw new ExternalException("Clipboard format is unavailable.");
        var size = GlobalSize(handle);
        if (size == 0 || size > (nuint)Math.Max(0, maximumBytes)) throw new ExternalException("Clipboard format is too large or unsupported.");
        var pointer = GlobalLock(handle);
        if (pointer == 0) throw new ExternalException("Clipboard data is unavailable.");
        try { var bytes = new byte[(int)size]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return bytes; }
        finally { GlobalUnlock(handle); }
    }
    public nint Allocate(byte[] bytes)
    {
        // GMEM_MOVEABLE is required, and successful SetClipboardData transfers
        // ownership to Windows. Failed/unpublished handles remain ours to free.
        var handle = GlobalAlloc(0x0002, (nuint)bytes.Length);
        if (handle == 0) return 0;
        var pointer = GlobalLock(handle);
        if (pointer == 0) { GlobalFree(handle); return 0; }
        try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
        catch { GlobalUnlock(handle); GlobalFree(handle); throw; }
        GlobalUnlock(handle);
        return handle;
    }
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("kernel32.dll", EntryPoint = "GetLocaleInfoW", CharSet = CharSet.Unicode)] private static extern int GetLocaleInfo(uint locale, uint type, out uint value, int characters);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int WideCharToMultiByte(uint codePage, uint flags, string text, int characters, [Out] byte[]? destination, int bytes, nint defaultCharacter, nint usedDefaultCharacter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern uint EnumClipboardFormats(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClipboardFormatName(uint format, StringBuilder name, int maximum);
    [DllImport("user32.dll")] private static extern nint GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern nint SetClipboardData(uint format, nint handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint handle);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint handle);
    [DllImport("user32.dll")] private static extern nint CopyImage(nint image, uint type, int width, int height, uint flags);
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")] private static extern int GetObject(nint handle, int bytes, out Bitmap bitmap);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint handle);
    [StructLayout(LayoutKind.Sequential)] private struct Bitmap
    {
        public int Type, Width, Height, WidthBytes;
        public ushort Planes, BitsPixel;
        public nint Bits;
    }
}
