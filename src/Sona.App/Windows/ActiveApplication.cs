using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Buffers.Binary;

namespace Sona.App.Windows;

internal sealed record ActiveApplication(nint Window, int ProcessId, string Name, long FocusEpoch, ImageSource? Icon = null);

/// <summary>Reads only the foreground window's process identity, never its title or text.</summary>
internal sealed class ActiveApplicationObserver : IDisposable
{
    private readonly DispatcherTimer timer;
    private readonly Func<ActiveApplication> read;
    private int cachedProcess;
    private string cachedName = "Unknown app";
    private string? cachedPath;
    private readonly ApplicationIconCache icons = new();
    private bool disposed;
    public ActiveApplication? Current { get; private set; }
    public event Action<ActiveApplication>? Changed;
    public ActiveApplicationObserver(Dispatcher dispatcher, Func<long> focusEpoch)
    {
        read = () => ReadForeground(focusEpoch());
        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += Tick;
    }
    internal ActiveApplicationObserver(Dispatcher dispatcher, Func<ActiveApplication> read)
    {
        this.read = read;
        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += Tick;
    }
    public void Start() { Refresh(); timer.Start(); }
    public void Refresh()
    {
        if (disposed) return;
        var snapshot = read();
        if (snapshot == Current) return;
        Current = snapshot; Changed?.Invoke(snapshot);
    }
    private void Tick(object? sender, EventArgs args) => Refresh();
    private ActiveApplication ReadForeground(long epoch)
    {
        var window = GetForegroundWindow();
        if (window == 0 || GetWindowThreadProcessId(window, out var pid) == 0) return new(0, 0, "No active app", epoch);
        if (cachedProcess != (int)pid || cachedName == "Unknown app")
        {
            cachedProcess = (int)pid;
            cachedPath = null;
            try
            {
                using var process = Process.GetProcessById(cachedProcess); cachedName = FriendlyName(process.ProcessName);
                try { cachedPath = process.MainModule?.FileName; }
                catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                cachedName = FriendlyName(cachedName, cachedPath);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { cachedName = "Unknown app"; }
        }
        return new(window, (int)pid, cachedName, epoch, icons.Get(cachedPath, window, (int)pid));
    }
    internal static string FriendlyName(string name, string? executablePath = null) => name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) && executablePath?.Replace('/', '\\').Contains(@"\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) == true ? "Codex"
        : name.Equals("codex", StringComparison.OrdinalIgnoreCase) ? "Codex"
        : name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) ? "ChatGPT"
        : name.Equals("discord", StringComparison.OrdinalIgnoreCase) ? "Discord" : name;
    public void Dispose() { disposed = true; timer.Stop(); timer.Tick -= Tick; Changed = null; icons.Dispose(); }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}

/// <summary>The active app can change; the recording destination never follows it.</summary>
internal sealed class RecordingDestinationContext
{
    public string ActiveAppName { get; private set; } = "No active app";
    public ImageSource ActiveAppIcon { get; private set; } = ApplicationIconCache.DefaultIcon;
    public ImageSource DestinationIcon { get; private set; } = ApplicationIconCache.DefaultIcon;
    public ImageSource Icon => IsRecordingContext && Destination != null ? DestinationIcon : ActiveAppIcon;
    public DraftTarget? Destination { get; private set; }
    public string DestinationName { get; private set; } = "No focused text field";
    public bool DestinationChanged { get; private set; }
    public bool IsRecordingContext { get; private set; }
    public string Label => !IsRecordingContext ? "Active app: " + ActiveAppName : Destination == null ? ActiveAppName
        : DestinationChanged ? "Draft stays in Sona · " + (ActiveAppName == DestinationName ? DestinationName + " focus changed" : DestinationName + " → " + ActiveAppName) : DestinationName;
    public void Observe(ActiveApplication app)
    {
        ActiveAppName = app.Name;
        ActiveAppIcon = app.Icon ?? ApplicationIconCache.DefaultIcon;
        if (IsRecordingContext && Destination != null && !DestinationChanged && app.Window == Destination.Window && app.ProcessId == Destination.OwnerProcessId && app.FocusEpoch == Destination.FocusEpoch && ReferenceEquals(DestinationIcon, ApplicationIconCache.DefaultIcon)) DestinationIcon = ActiveAppIcon;
        if (IsRecordingContext && Destination != null && (app.Window != Destination.Window || app.ProcessId != Destination.OwnerProcessId || app.FocusEpoch != Destination.FocusEpoch)) DestinationChanged = true;
    }
    public void Begin(DraftTarget? destination, string name, ImageSource? icon = null)
    {
        Destination = destination; DestinationName = name; DestinationIcon = icon ?? ApplicationIconCache.DefaultIcon; DestinationChanged = false; IsRecordingContext = true;
    }
    public void End() { IsRecordingContext = false; Destination = null; DestinationChanged = false; }
}

/// <summary>At most one local extraction in flight and 32 frozen icons retained. No shell/network lookup.</summary>
internal sealed class ApplicationIconCache : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, ImageSource> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> order = new();
    private readonly Func<string, nint, int, ImageSource> extract;
    private readonly int capacity;
    private Task pending = Task.CompletedTask;
    private bool busy, disposed;
    public static ImageSource DefaultIcon { get; } = CreateDefault();
    internal Task Pending { get { lock (gate) return pending; } }
    internal int Count { get { lock (gate) return cache.Count; } }
    public ApplicationIconCache() : this(Extract, 32) { }
    internal ApplicationIconCache(Func<string, ImageSource> extract, int capacity) : this((path, _, _) => extract(path), capacity) { }
    internal ApplicationIconCache(Func<string, nint, int, ImageSource> extract, int capacity) { this.extract = extract; this.capacity = Math.Clamp(capacity, 1, 32); }
    public ImageSource Get(string? path, nint window = 0, int processId = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return DefaultIcon;
        lock (gate)
        {
            if (disposed) return DefaultIcon;
            var key = window == 0 ? path : $"{path}|{processId}|{window}";
            if (cache.TryGetValue(key, out var icon)) return icon;
            if (busy) return DefaultIcon;
            busy = true;
            pending = Task.Run(() =>
            {
                ImageSource icon;
                try { icon = extract(path, window, processId); if (!icon.IsFrozen) { if (icon.CanFreeze) icon.Freeze(); else icon = DefaultIcon; } }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or ExternalException or XmlException or FormatException or NotSupportedException or UnauthorizedAccessException) { icon = DefaultIcon; }
                lock (gate)
                {
                    busy = false;
                    if (disposed) return;
                    if (cache.Count >= capacity) cache.Remove(order.Dequeue());
                    cache[key] = icon; order.Enqueue(key);
                }
            });
            return DefaultIcon;
        }
    }
    private static ImageSource Extract(string path, nint window, int processId)
    {
        nint large = 0, small = 0;
        try
        {
            // Mapped network drives and remote executable paths are never consulted.
            if (GetDriveType(Path.GetPathRoot(path)!) is not (3 or 6)) return DefaultIcon;
            // Packaged desktop apps can retain a legacy executable/window mark. The
            // installed package logo remains the authoritative identity for Codex.
            var package = PackageIcon(path);
            if (!ReferenceEquals(package, DefaultIcon)) return package;
            var activeWindow = WindowIcon(window, processId);
            if (!ReferenceEquals(activeWindow, DefaultIcon)) return activeWindow;
            // With both output arrays, Windows can report two extracted handles
            // for one resource group. Require a usable handle, not exactly one.
            var count = ExtractIconEx(path, 0, out large, out small, 1);
            if (count is 0 or uint.MaxValue || (large == 0 && small == 0)) return DefaultIcon;
            var handle = small != 0 ? small : large;
            if (handle == 0) return DefaultIcon;
            var source = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); return source;
        }
        finally { if (small != 0) DestroyIcon(small); if (large != 0 && large != small) DestroyIcon(large); }
    }
    private static ImageSource WindowIcon(nint window, int processId)
    {
        if (!OwnedWindow(window, processId)) return DefaultIcon;
        // Icon metadata only. Never ask a foreign app for its title, text or DOM.
        foreach (var kind in new[] { 0, 1, 2 })
        {
            if (SendMessageTimeout(window, 0x007F, (nint)kind, 0, 0x22, 60, out var handle) == 0 || handle == 0) continue;
            var icon = CopyWindowIcon(window, processId, handle);
            if (!ReferenceEquals(icon, DefaultIcon)) return icon;
        }
        foreach (var index in new[] { -34, -14 })
        {
            var icon = CopyWindowIcon(window, processId, GetClassLongPtr(window, index));
            if (!ReferenceEquals(icon, DefaultIcon)) return icon;
        }
        return DefaultIcon;
    }
    private static bool OwnedWindow(nint window, int processId) => window != 0 && processId > 0 && GetWindowThreadProcessId(window, out var actual) != 0 && actual == (uint)processId;
    private static ImageSource CopyWindowIcon(nint window, int processId, nint handle)
    {
        if (handle == 0 || !OwnedWindow(window, processId)) return DefaultIcon;
        // Window/class handles belong to the other app. Destroy only our copy.
        var copy = CopyIcon(handle);
        if (copy == 0) return DefaultIcon;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(copy, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); return source;
        }
        finally { DestroyIcon(copy); }
    }
    private static ImageSource PackageIcon(string executable)
    {
        var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps") + Path.DirectorySeparatorChar;
        if (!executable.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase)) return DefaultIcon;
        var packageName = executable[windowsApps.Length..].Split(Path.DirectorySeparatorChar)[0];
        var package = Path.Combine(windowsApps, packageName);
        var manifest = Path.Combine(package, "AppxManifest.xml");
        if (!File.Exists(manifest) || new FileInfo(manifest).Length > 262144) return DefaultIcon;
        using var reader = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 });
        var document = XDocument.Load(reader);
        var relativeExecutable = Path.GetRelativePath(package, executable);
        var application = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Application" && string.Equals(element.Attribute("Executable")?.Value.Replace('/', Path.DirectorySeparatorChar), relativeExecutable, StringComparison.OrdinalIgnoreCase));
        var logo = application?.Descendants().Select(element => element.Attribute("Square44x44Logo")?.Value).FirstOrDefault(value => value != null);
        if (logo == null || Path.IsPathRooted(logo) || logo.Split('/', '\\').Any(part => part == "..")) return DefaultIcon;
        var asset = Path.GetFullPath(Path.Combine(package, logo.Replace('/', Path.DirectorySeparatorChar)));
        if (!asset.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !File.Exists(asset) || new FileInfo(asset).Length > 2097152) return DefaultIcon;
        if ((File.GetAttributes(asset) & FileAttributes.ReparsePoint) != 0) return DefaultIcon;
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(asset)!); directory != null && directory.FullName.StartsWith(package, StringComparison.OrdinalIgnoreCase); directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return DefaultIcon;
        using var stream = File.OpenRead(asset);
        var header = new byte[24]; stream.ReadExactly(header);
        if (!header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4)) is < 1 or > 1024 || BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4)) is < 1 or > 1024) return DefaultIcon;
        stream.Position = 0;
        var source = new BitmapImage(); source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.DecodePixelWidth = 32; source.StreamSource = stream; source.EndInit(); source.Freeze(); return source;
    }
    private static ImageSource CreateDefault()
    {
        var drawing = new GeometryDrawing(Brushes.Transparent, new Pen(new SolidColorBrush(Color.FromRgb(164, 151, 183)), 1.5), new RectangleGeometry(new Rect(2, 3, 12, 10), 2, 2));
        var source = new DrawingImage(drawing); source.Freeze(); return source;
    }
    public void Dispose() { lock (gate) { disposed = true; cache.Clear(); order.Clear(); } }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")] private static extern nint SendMessageTimeout(nint window, uint message, nint wparam, nint lparam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern nint GetClassLongPtr(nint window, int index);
    [DllImport("user32.dll")] private static extern nint CopyIcon(nint icon);
    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string path, int index, out nint large, out nint small, uint count);
    [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode)] private static extern uint GetDriveType(string root);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
}
